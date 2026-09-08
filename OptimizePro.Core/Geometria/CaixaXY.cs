namespace OptimizePro.Core;

public readonly record struct CaixaXY(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Largura => MaxX - MinX;
    public double Altura => MaxY - MinY;
}
