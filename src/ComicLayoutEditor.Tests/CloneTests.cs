using ComicLayoutEditor.Core.Models;
using Xunit;

namespace ComicLayoutEditor.Tests;

/// <summary>
/// Verifica que <see cref="Page.Clone"/> produce una copia profunda e independiente
/// (usada al duplicar páginas en la Fase 5).
/// </summary>
public class CloneTests
{
    [Fact]
    public void ClonePage_CopiesValues_WithNewIds()
    {
        var original = TestDocuments.CreateRich().Pages[0];

        var clone = original.Clone();

        // Mismos valores geométricos y de contenido...
        Assert.Equal(original.SizeMm, clone.SizeMm);
        Assert.Equal(original.Panels.Count, clone.Panels.Count);

        var srcPanel = original.Panels[0];
        var dstPanel = clone.Panels[0];
        Assert.Equal(srcPanel.Bounds, dstPanel.Bounds);
        Assert.Equal(srcPanel.ImageFit, dstPanel.ImageFit);
        Assert.Equal(srcPanel.ImageZoom, dstPanel.ImageZoom);
        Assert.Equal(srcPanel.ImageOffset, dstPanel.ImageOffset);
        Assert.Equal(srcPanel.Image!.RelativePath, dstPanel.Image!.RelativePath);
        Assert.Equal(srcPanel.Balloons[0].Text, dstPanel.Balloons[0].Text);
        Assert.Equal(srcPanel.Balloons[0].TailPoint, dstPanel.Balloons[0].TailPoint);

        // ...pero con identidades nuevas (no comparte Id de página ni de viñeta/bocadillo).
        Assert.NotEqual(original.Id, clone.Id);
        Assert.NotEqual(srcPanel.Id, dstPanel.Id);
        Assert.NotEqual(srcPanel.Balloons[0].Id, dstPanel.Balloons[0].Id);
    }

    [Fact]
    public void ClonePage_IsIndependent_FromOriginal()
    {
        var original = TestDocuments.CreateRich().Pages[0];
        var clone = original.Clone();

        // Mutar el clon no debe afectar al original.
        clone.Panels[0].Balloons[0].Text = "cambiado";
        clone.Panels.Add(new Panel());

        Assert.Equal("¡Hola, mundo!", original.Panels[0].Balloons[0].Text);
        Assert.NotEqual(original.Panels.Count, clone.Panels.Count);
    }
}
