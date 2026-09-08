namespace OptimizePro.Core.Moldes;

/// <summary>
/// Transformação afim 2D (x' = A*x + C*y + E; y' = B*x + D*y + F) — compartilhada entre
/// leitores de formato: resolve INSERT do DXF (§8.1) e transform/&lt;use&gt; do SVG
/// (mesmo layout de <c>matrix(a,b,c,d,e,f)</c> do SVG: A=a, B=b, C=c, D=d, E=e, F=f).
/// </summary>
public readonly struct Transformacao2D
{
    public double A { get; init; }
    public double B { get; init; }
    public double C { get; init; }
    public double D { get; init; }
    public double E { get; init; }
    public double F { get; init; }

    public static Transformacao2D Identidade => new() { A = 1, D = 1 };

    public static Transformacao2D DeMatriz(double a, double b, double c, double d, double e, double f) =>
        new() { A = a, B = b, C = c, D = d, E = e, F = f };

    public static Transformacao2D DeEscalaRotacaoTranslacao(
        double escalaX, double escalaY, double rotacaoRad, double deslocX, double deslocY)
    {
        var cos = Math.Cos(rotacaoRad);
        var sin = Math.Sin(rotacaoRad);

        return new Transformacao2D
        {
            A = cos * escalaX,
            C = -sin * escalaY,
            E = deslocX,
            B = sin * escalaX,
            D = cos * escalaY,
            F = deslocY,
        };
    }

    public PontoXY Aplicar(PontoXY p) => new(A * p.X + C * p.Y + E, B * p.X + D * p.Y + F);

    /// <summary>Composição tal que <c>this.ComposicaoCom(interna).Aplicar(p) == this.Aplicar(interna.Aplicar(p))</c>.</summary>
    public Transformacao2D ComposicaoCom(Transformacao2D interna) => new()
    {
        A = A * interna.A + C * interna.B,
        B = B * interna.A + D * interna.B,
        C = A * interna.C + C * interna.D,
        D = B * interna.C + D * interna.D,
        E = A * interna.E + C * interna.F + E,
        F = B * interna.E + D * interna.F + F,
    };
}
