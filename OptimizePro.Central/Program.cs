using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OptimizePro.Central;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CentralDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Central")
        ?? throw new InvalidOperationException("Configure a connection string \"Central\" (Postgres) em appsettings/variável de ambiente.")));

builder.Services.AddScoped<IInstalacaoRepository, InstalacaoRepository>();
builder.Services.AddScoped<IInstalacaoService, InstalacaoService>();
builder.Services.AddScoped<IDadoSincronizadoRepository, DadoSincronizadoRepository>();
builder.Services.AddScoped<IAutenticacaoDeUsuarioService, AutenticacaoDeUsuarioService>();

var chaveSecretaDoJwt = builder.Configuration["Jwt:ChaveSecreta"]
    ?? throw new InvalidOperationException("Configure \"Jwt:ChaveSecreta\" via `dotnet user-secrets set Jwt:ChaveSecreta \"...\"` (mín. 32 caracteres).");
var configuracaoDoJwt = new ConfiguracaoDoJwt { ChaveSecreta = chaveSecretaDoJwt };
builder.Services.AddSingleton(configuracaoDoJwt);
builder.Services.AddSingleton<EmissorDeToken>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Sem isto, o handler remapeia "sub"/"name" pros URIs longos de ClaimTypes por baixo
        // dos panos (comportamento herdado do WS-Federation) — os claims chegam com um nome
        // diferente do que EmissorDeToken escreveu, e toda leitura por JwtRegisteredClaimNames
        // (ex.: em /api/auth/me) vem null silenciosamente. Achado testando de verdade contra o
        // Supabase — não aparece em teste unitário que só olha o JWT cru.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuracaoDoJwt.Emissor,
            ValidateAudience = true,
            ValidAudience = configuracaoDoJwt.Emissor,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(chaveSecretaDoJwt)),
        };
    });
builder.Services.AddAuthorization();

// CORS liberado pro front-end React chamar de outra origem (§24.5) — a Central só serve API,
// nunca HTML/cookies de sessão, então não há CSRF a se preocupar aqui.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Autenticação por chave de API (§24.1/§24.2) — dois headers em vez de Bearer/Basic porque o
// cliente sincronizador (app desktop) já sabe os dois valores separados (guardados juntos,
// DPAPI-protegidos, no mesmo lugar que a licença) e não há sessão nem usuário humano aqui, só
// instalação-pra-instalação. Só o endpoint de sync usa isto por enquanto; se crescer pra mais
// endpoints autenticados, vira um AuthenticationHandler de verdade.
static async Task<Instalacao?> AutenticarRequisicaoAsync(HttpRequest requisicao, IInstalacaoService instalacoes)
{
    if (!requisicao.Headers.TryGetValue("X-Instalacao-Id", out var instalacaoId) ||
        !requisicao.Headers.TryGetValue("X-Chave-Api", out var chaveDeApi))
        return null;

    return await instalacoes.AutenticarAsync(instalacaoId.ToString(), chaveDeApi.ToString());
}

// Provisiona (ou reencontra, de forma idempotente) a instalação de uma fábrica a partir do
// ClienteIdHash já embutido na licença dela (§24.1) — chamado automaticamente pelo app
// desktop logo depois de uma ativação de licença bem-sucedida, best-effort (sem internet,
// simplesmente não sincroniza ainda; a licença em si continua 100% offline).
app.MapPost("/api/instalacoes/provisionar", async (RequisicaoDeProvisionamento corpo, IInstalacaoService instalacoes) =>
{
    var resultado = await instalacoes.ProvisionarAsync(corpo.ClienteIdHash, corpo.NomeDaFabrica);
    return Results.Ok(new
    {
        instalacaoId = resultado.Instalacao.Id,
        codigo = resultado.Instalacao.Codigo,
        chaveDeApi = resultado.ChaveDeApi,
        jaExistia = resultado.JaExistia,
    });
});

// Recebe o lote periódico do sincronizador do app desktop (§24.2) — cada instalação manda o
// estado atual dos itens que mudaram desde a última vez; a Central só guarda (upsert), não
// interpreta o conteúdo de cada `DadosJson` (isso é problema de quem lê, na fase de dashboard).
app.MapPost("/api/sync/lote", async (HttpRequest requisicao, RequisicaoDeSincronizacao corpo, IInstalacaoService instalacoes, IDadoSincronizadoRepository dados) =>
{
    var instalacao = await AutenticarRequisicaoAsync(requisicao, instalacoes);
    if (instalacao is null) return Results.Unauthorized();

    if (corpo.Itens.Count == 0) return Results.Ok(new { recebidos = 0 });

    await dados.SalvarLoteAsync(instalacao.Id, corpo.Itens, default);
    await instalacoes.RegistrarSincronizacaoAsync(instalacao.Id);

    return Results.Ok(new { recebidos = corpo.Itens.Count });
});

// Login do painel remoto (§24.4) — empresa (o Codigo curto de 6 chars) + login + senha da
// pessoa. Devolve um JWT com os módulos liberados dela já dentro, pro React montar o menu sem
// precisar de outra chamada.
app.MapPost("/api/auth/login", async (RequisicaoDeLogin corpo, IAutenticacaoDeUsuarioService autenticacao, EmissorDeToken emissor) =>
{
    var resultado = await autenticacao.AutenticarAsync(corpo.Codigo, corpo.Login, corpo.Senha);
    if (!resultado.Sucesso || resultado.Usuario is null)
        return Results.Json(new { erro = resultado.Erro }, statusCode: StatusCodes.Status401Unauthorized);

    var token = emissor.Emitir(resultado.Usuario);
    return Results.Ok(new
    {
        token,
        usuario = new
        {
            resultado.Usuario.UsuarioId,
            resultado.Usuario.Login,
            resultado.Usuario.Nome,
            resultado.Usuario.EhAdministrador,
            resultado.Usuario.ModulosLiberados,
        },
    });
});

// Só pra confirmar que um token é válido (o React chama isso ao carregar, pra saber se a
// sessão guardada ainda vale) — devolve os mesmos claims que já estavam no token.
app.MapGet("/api/auth/me", (ClaimsPrincipal usuario) =>
{
    var modulos = usuario.FindAll(ClaimsDoPainel.ModuloLiberado).Select(c => c.Value).ToList();
    return Results.Ok(new
    {
        usuarioId = usuario.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub),
        nome = usuario.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name),
        login = usuario.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.PreferredUsername),
        instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId),
        ehAdministrador = usuario.FindFirstValue(ClaimsDoPainel.EhAdministrador) == "true",
        modulosLiberados = modulos,
    });
}).RequireAuthorization();

app.Run();

public sealed record RequisicaoDeProvisionamento(uint ClienteIdHash, string? NomeDaFabrica);
public sealed record RequisicaoDeSincronizacao(List<ItemSincronizado> Itens);
public sealed record RequisicaoDeLogin(string Codigo, string Login, string Senha);
