using ComicLayoutEditor.Core.Models;
using ComicLayoutEditor.Core.Serialization;

namespace ComicLayoutEditor.Tests;

public class ComicJsonTests
{
    [Fact]
    public void RoundTrip_PreservesAllData()
    {
        var original = TestDocuments.CreateRich();

        var json = ComicJson.Serialize(original);
        var restored = ComicJson.Deserialize(json);

        // Comparación robusta: re-serializar y comparar el JSON resultante.
        Assert.Equal(json, ComicJson.Serialize(restored));
    }

    [Fact]
    public void RoundTrip_PreservesKeyValues()
    {
        var original = TestDocuments.CreateRich();

        var restored = ComicJson.Deserialize(ComicJson.Serialize(original));

        Assert.Equal("Mi historieta", restored.Title);
        Assert.Equal(2, restored.Pages.Count);

        var page1 = restored.Pages[0];
        Assert.Equal(2, page1.Panels.Count);

        var panel = page1.Panels[0];
        Assert.Equal(original.Pages[0].Panels[0].Id, panel.Id);
        Assert.Equal(new RectD(0.05, 0.05, 0.4, 0.3), panel.Bounds);
        Assert.Equal(ImageFit.Contain, panel.ImageFit);
        Assert.NotNull(panel.Image);
        Assert.Equal("img_0001.png", panel.Image!.RelativePath);
        Assert.Equal(1.75, panel.ImageZoom);
        Assert.Equal(new PointD(-0.1, 0.2), panel.ImageOffset);

        var balloon = panel.Balloons[0];
        Assert.Equal(BalloonShape.Thought, balloon.Shape);
        Assert.Equal("¡Hola, mundo!", balloon.Text);
        Assert.Equal(new PointD(0.3, 0.9), balloon.TailPoint);

        // El panel vacío conserva sus nulos.
        var empty = page1.Panels[1];
        Assert.Null(empty.Image);
        Assert.Empty(empty.Balloons);

        // La página en horizontal conserva el tamaño intercambiado.
        Assert.Equal(new SizeD(Page.A4HeightMm, Page.A4WidthMm), restored.Pages[1].SizeMm);
    }

    [Fact]
    public void RoundTrip_PreservesRichText()
    {
        var doc = new ComicDocument();
        var page = new Page();
        var panel = new Panel { Bounds = new RectD(0, 0, 1, 1) };
        panel.Balloons.Add(new Balloon
        {
            Text = "Hola MUNDO",
            RichText = new List<TextRun>
            {
                new() { Text = "Hola " },
                new() { Text = "MUNDO", Bold = true, Italic = true, Underline = true, FontFamily = "Georgia", FontSize = 20, Color = "#E53935" }
            }
        });
        page.Panels.Add(panel);
        doc.Pages.Add(page);

        var restored = ComicJson.Deserialize(ComicJson.Serialize(doc));
        var runs = restored.Pages[0].Panels[0].Balloons[0].RichText;

        Assert.NotNull(runs);
        Assert.Equal(2, runs!.Count);
        Assert.Equal("MUNDO", runs[1].Text);
        Assert.True(runs[1].Bold);
        Assert.True(runs[1].Italic);
        Assert.True(runs[1].Underline);
        Assert.Equal("Georgia", runs[1].FontFamily);
        Assert.Equal(20, runs[1].FontSize);
        Assert.Equal("#E53935", runs[1].Color);
    }

    [Fact]
    public void Deserialize_BalloonWithoutRichText_LeavesItNull()
    {
        // Archivo antiguo: bocadillo con solo texto plano, sin "richText".
        var json = """
        {
          "pages": [
            { "panels": [
              { "balloons": [ { "text": "Texto plano" } ] }
            ] }
          ]
        }
        """;

        var restored = ComicJson.Deserialize(json);
        var balloon = restored.Pages[0].Panels[0].Balloons[0];

        Assert.Equal("Texto plano", balloon.Text);
        Assert.Null(balloon.RichText);
    }

    [Fact]
    public void Serialize_WritesEnumsAsStrings()
    {
        var doc = TestDocuments.CreateRich();

        var json = ComicJson.Serialize(doc);

        Assert.Contains("\"Thought\"", json);
        Assert.Contains("\"Contain\"", json);
        Assert.DoesNotContain("\"shape\": 3", json);
    }
}
