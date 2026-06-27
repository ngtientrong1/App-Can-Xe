using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public class AutoCompleteSelectionLogicTests
{
    private static readonly string[] Items = ["Công ty Alpha", "Công ty Beta", "Cà tươi"];

    [Fact]
    public void ArrowDown_Enter_CommitsHighlightedItem()
    {
        var index = AutoCompleteSelectionLogic.MoveHighlight(-1, Items.Length, 1);
        var text = AutoCompleteSelectionLogic.ResolveCommitText("cong", null, index, Items);
        Assert.Equal("Công ty Alpha", text);
    }

    [Fact]
    public void Tab_WithHighlightedItem_CommitsSelection()
    {
        const int index = 1;
        Assert.True(AutoCompleteSelectionLogic.HasHighlightedSelection(index, Items.Length));
        var text = AutoCompleteSelectionLogic.ResolveCommitText("beta", null, index, Items);
        Assert.Equal("Công ty Beta", text);
    }

    [Fact]
    public void Tab_WithoutSelection_KeepsTypedText()
    {
        const string typed = "Khách mới XYZ";
        var text = AutoCompleteSelectionLogic.ResolveCommitText(typed, null, -1, Items);
        Assert.Equal(typed, text);
    }

    [Fact]
    public void Enter_CommitsCargoTypeSuggestion()
    {
        var index = AutoCompleteSelectionLogic.MoveHighlight(1, Items.Length, 1);
        var text = AutoCompleteSelectionLogic.ResolveCommitText("ca", null, index, Items);
        Assert.Equal("Cà tươi", text);
    }
}
