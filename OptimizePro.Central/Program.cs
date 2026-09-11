using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OptimizePro.Central;

var builder = WebApplication.CreateBuilder(args);

// Hospedagem tipo Railway/Fly.io atribui a porta dinamicamente via variável de ambiente
// "PORT" (não é a mesma coisa que ASPNETCORE_URLS) — sem isto, o Kestrel escuta numa porta
// fixa que a plataforma não sabe rotear pra fora. Em dev local (sem PORT setada), não muda
// nada: continua obedecendo --urls/appsettings normalmente.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } porta)
    builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

builder.Services.AddDbContext<CentralDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Central")
        ?? throw new InvalidOperationException("Configure a connection string \"Central\" (Postgres) em appsettings/variável de ambiente.")));

builder.Services.AddScoped<IInstalacaoRepository, InstalacaoRepository>();
builder.Services.AddScoped<IInstalacaoService, InstalacaoService>();
builder.Services.AddScoped<IDadoSincronizadoRepository, DadoSincronizadoRepository>();
builder.Services.AddScoped<IAutenticacaoDeUsuarioService, AutenticacaoDeUsuarioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUsuarioAdminService, UsuarioAdminService>();
builder.Services.AddScoped<IFaturamentoService, FaturamentoService>();
builder.Services.AddScoped<IAdministradorRepository, AdministradorRepository>();
builder.Services.AddScoped<IAdministradorService, AdministradorService>();
builder.Services.AddScoped<IPainelDeStaffService, PainelDeStaffService>();

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

// Pull de usuários pro cache local do app desktop (§25 — login do desktop espelhando os
// módulos do painel remoto). Mesma autenticação de "/api/sync/lote" (chave de API,
// instalação-pra-instalação) — não é o login humano de "/api/auth/login". Devolve os dados
// crus, COM hash+sal de senha: o desktop precisa validar login OFFLINE (sem depender de rede
// no chão de fábrica), então precisa de material suficiente pra conferir a senha sozinho.
app.MapGet("/api/sync/usuarios", async (HttpRequest requisicao, IInstalacaoService instalacoes, IUsuarioAdminService usuarios) =>
{
    var instalacao = await AutenticarRequisicaoAsync(requisicao, instalacoes);
    if (instalacao is null) return Results.Unauthorized();

    return Results.Ok(await usuarios.ListarAsync(instalacao.Id));
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

// Telas de dashboard do painel remoto (§24.6) — cada uma exige o módulo correspondente
// liberado no token (não "é administrador": módulo operacional é concedido por si só, não
// por ser dono). Nunca fala com o PC da fábrica — só lê o espelho já sincronizado.
static IResult? ExigirModulo(ClaimsPrincipal usuario, string modulo)
{
    var liberado = usuario.FindAll(ClaimsDoPainel.ModuloLiberado).Any(c => c.Value == modulo);
    return liberado ? null : Results.Json(new { erro = $"Módulo \"{modulo}\" não liberado pra este usuário." }, statusCode: StatusCodes.Status403Forbidden);
}

var apiDoDashboard = app.MapGroup("/api/dashboard").RequireAuthorization();

apiDoDashboard.MapGet("/maquinas", async (ClaimsPrincipal usuario, IDashboardService dashboard) =>
{
    if (ExigirModulo(usuario, "Maquinas") is { } bloqueado) return bloqueado;
    return Results.Ok(await dashboard.ObterMaquinasAsync(usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!));
});

apiDoDashboard.MapGet("/impressoras", async (ClaimsPrincipal usuario, IDashboardService dashboard) =>
{
    if (ExigirModulo(usuario, "Impressoras") is { } bloqueado) return bloqueado;
    return Results.Ok(await dashboard.ObterImpressorasAsync(usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!));
});

apiDoDashboard.MapGet("/historico", async (ClaimsPrincipal usuario, IDashboardService dashboard) =>
{
    if (ExigirModulo(usuario, "Historico") is { } bloqueado) return bloqueado;
    return Results.Ok(await dashboard.ObterHistoricoAsync(usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!));
});

apiDoDashboard.MapGet("/reposicao", async (ClaimsPrincipal usuario, IDashboardService dashboard) =>
{
    if (ExigirModulo(usuario, "Reposicao") is { } bloqueado) return bloqueado;
    return Results.Ok(await dashboard.ObterReposicaoAsync(usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!));
});

apiDoDashboard.MapGet("/pedidos", async (ClaimsPrincipal usuario, IDashboardService dashboard) =>
{
    if (ExigirModulo(usuario, "Pedidos") is { } bloqueado) return bloqueado;
    return Results.Ok(await dashboard.ObterPedidosAsync(usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!));
});

apiDoDashboard.MapGet("/ordens-servico", async (ClaimsPrincipal usuario, IDashboardService dashboard) =>
{
    if (ExigirModulo(usuario, "OrdensDeServico") is { } bloqueado) return bloqueado;
    return Results.Ok(await dashboard.ObterOrdensDeServicoAsync(usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!));
});

// Gestão de usuários do painel remoto (§24.7) — só quem é administrador mexe aqui; módulo
// operacional liberado não dá acesso (mesma regra de ExigirModulo, na direção oposta). A
// Central é quem manda em Usuario a partir daqui: o app desktop nunca teve tela própria pra
// isso, então não existe reconciliação de escrita local pra fazer (ver comentário em
// IUsuarioAdminService).
static IResult? ExigirAdministrador(ClaimsPrincipal usuario)
{
    var ehAdministrador = usuario.FindFirstValue(ClaimsDoPainel.EhAdministrador) == "true";
    return ehAdministrador ? null : Results.Json(new { erro = "Só administradores gerenciam usuários." }, statusCode: StatusCodes.Status403Forbidden);
}

static object ParaResposta(UsuarioSincronizadoDto u) => new
{
    u.Id, u.Login, u.Nome, u.EhAdministrador, u.Habilitado, u.ModulosLiberados,
};

// Bootstrap do primeiro administrador (§24.7) — sem token, de propósito: é a única porta de
// entrada possível já que a Central virou fonte de verdade de Usuario (não existe mais tela
// de cadastro no desktop cujo push pudesse semear o primeiro usuário). Fecha sozinho depois
// do primeiro cadastro bem-sucedido (ver JaTemUsuarioException) — não é um backdoor permanente.
app.MapPost("/api/usuarios/bootstrap", async (RequisicaoDeBootstrap corpo, IInstalacaoRepository instalacoes, IUsuarioAdminService usuarios) =>
{
    var instalacao = await instalacoes.ObterPorCodigoAsync(corpo.Codigo.Trim().ToUpperInvariant());
    if (instalacao is null) return Results.NotFound(new { erro = "Código de instalação não encontrado." });

    try
    {
        var criado = await usuarios.BootstrapAsync(instalacao.Id, corpo.Login, corpo.Nome, corpo.Senha);
        return Results.Ok(ParaResposta(criado));
    }
    catch (JaTemUsuarioException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status409Conflict);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

var apiDeUsuarios = app.MapGroup("/api/usuarios").RequireAuthorization();

apiDeUsuarios.MapGet("/", async (ClaimsPrincipal usuario, IUsuarioAdminService usuarios) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;
    var lista = await usuarios.ListarAsync(instalacaoId);
    return Results.Ok(lista.Select(ParaResposta));
});

apiDeUsuarios.MapPost("/", async (ClaimsPrincipal usuario, RequisicaoDeCadastroDeUsuario corpo, IUsuarioAdminService usuarios) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;
    try
    {
        var criado = await usuarios.CadastrarAsync(instalacaoId, corpo.Login, corpo.Nome, corpo.Senha, corpo.ModulosLiberados, corpo.EhAdministrador);
        return Results.Ok(ParaResposta(criado));
    }
    catch (LoginJaExisteException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status409Conflict);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

apiDeUsuarios.MapPut("/{id}", async (ClaimsPrincipal usuario, string id, RequisicaoDeEdicaoDeUsuario corpo, IUsuarioAdminService usuarios) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;

    // Ninguém tira o próprio "administrador" por engano e fica trancado pra fora da própria
    // tela de usuários — só outro administrador pode rebaixar alguém.
    if (id == usuario.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) && !corpo.EhAdministrador)
        return Results.Json(new { erro = "Você não pode remover seu próprio acesso de administrador." }, statusCode: StatusCodes.Status400BadRequest);

    var ok = await usuarios.AtualizarAsync(instalacaoId, id, corpo.Nome, corpo.ModulosLiberados, corpo.EhAdministrador);
    return ok ? Results.Ok() : Results.NotFound();
});

apiDeUsuarios.MapPost("/{id}/redefinir-senha", async (ClaimsPrincipal usuario, string id, RequisicaoDeRedefinicaoDeSenha corpo, IUsuarioAdminService usuarios) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;
    try
    {
        var ok = await usuarios.RedefinirSenhaAsync(instalacaoId, id, corpo.NovaSenha);
        return ok ? Results.Ok() : Results.NotFound();
    }
    catch (ArgumentException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

apiDeUsuarios.MapPut("/{id}/habilitado", async (ClaimsPrincipal usuario, string id, RequisicaoDeHabilitado corpo, IUsuarioAdminService usuarios) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;

    if (id == usuario.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) && !corpo.Habilitado)
        return Results.Json(new { erro = "Você não pode desativar sua própria conta." }, statusCode: StatusCodes.Status400BadRequest);

    var ok = await usuarios.AtualizarHabilitadoAsync(instalacaoId, id, corpo.Habilitado);
    return ok ? Results.Ok() : Results.NotFound();
});

apiDeUsuarios.MapDelete("/{id}", async (ClaimsPrincipal usuario, string id, IUsuarioAdminService usuarios) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;

    if (id == usuario.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub))
        return Results.Json(new { erro = "Você não pode excluir sua própria conta." }, statusCode: StatusCodes.Status400BadRequest);

    var ok = await usuarios.ExcluirAsync(instalacaoId, id);
    return ok ? Results.Ok() : Results.NotFound();
});

// Tela de Faturamento do painel remoto (§24.8) — mensalidade + "vence em", só administrador
// (mesma regra de acesso de /api/usuarios: não é módulo operacional).
app.MapGet("/api/faturamento", async (ClaimsPrincipal usuario, IFaturamentoService faturamento) =>
{
    if (ExigirAdministrador(usuario) is { } bloqueado) return bloqueado;
    var instalacaoId = usuario.FindFirstValue(ClaimsDoPainel.InstalacaoId)!;
    var resumo = await faturamento.ObterAsync(instalacaoId);
    return resumo is null
        ? Results.Json(new { erro = "Ainda não há dados de faturamento sincronizados desta instalação." }, statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(resumo);
}).RequireAuthorization();

// Painel de staff (§26) — a Codeex Solutions gerenciando os clientes do OptimizePro, de fora
// de qualquer instalação específica. Login/bootstrap abaixo continuam existindo (émite token
// com o claim "papel=staff"), mas hoje nenhum endpoint exige esse token — ver
// /api/staff/instalacoes logo abaixo.

// Bootstrap do primeiro administrador de staff — mesmo raciocínio do bootstrap de usuário
// (§24.7): só existe essa porta de entrada enquanto não houver NENHUM staff cadastrado; fecha
// sozinho depois do primeiro cadastro (JaTemAdministradorException, 409).
app.MapPost("/api/staff/bootstrap", async (RequisicaoDeBootstrapDeStaff corpo, IAdministradorService administradores) =>
{
    try
    {
        var criado = await administradores.BootstrapAsync(corpo.Email, corpo.Nome, corpo.Senha);
        return Results.Ok(new { criado.Id, criado.Email, criado.Nome });
    }
    catch (JaTemAdministradorException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status409Conflict);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/staff/login", async (RequisicaoDeLoginDeStaff corpo, IAdministradorService administradores, EmissorDeToken emissor) =>
{
    var resultado = await administradores.AutenticarAsync(corpo.Email, corpo.Senha);
    if (!resultado.Sucesso || resultado.Administrador is null)
        return Results.Json(new { erro = resultado.Erro }, statusCode: StatusCodes.Status401Unauthorized);

    var token = emissor.EmitirParaStaff(resultado.Administrador);
    return Results.Ok(new { token, administrador = resultado.Administrador });
});

// Sem autenticação, de propósito (10/09/2026, decisão do usuário) — só a Codeex Solutions
// tem esse link, e o login de staff (acima) virou infraestrutura sem uso: mantida no backend
// caso essa decisão mude, mas nada mais a exige.
app.MapGet("/api/staff/instalacoes", async (IPainelDeStaffService painel) =>
    Results.Ok(await painel.ListarInstalacoesAsync()));

app.Run();

public sealed record RequisicaoDeProvisionamento(uint ClienteIdHash, string? NomeDaFabrica);
public sealed record RequisicaoDeSincronizacao(List<ItemSincronizado> Itens);
public sealed record RequisicaoDeLogin(string Codigo, string Login, string Senha);
public sealed record RequisicaoDeBootstrap(string Codigo, string Login, string Nome, string Senha);
public sealed record RequisicaoDeCadastroDeUsuario(string Login, string Nome, string Senha, List<string> ModulosLiberados, bool EhAdministrador);
public sealed record RequisicaoDeEdicaoDeUsuario(string Nome, List<string> ModulosLiberados, bool EhAdministrador);
public sealed record RequisicaoDeRedefinicaoDeSenha(string NovaSenha);
public sealed record RequisicaoDeHabilitado(bool Habilitado);
public sealed record RequisicaoDeBootstrapDeStaff(string Email, string Nome, string Senha);
public sealed record RequisicaoDeLoginDeStaff(string Email, string Senha);
