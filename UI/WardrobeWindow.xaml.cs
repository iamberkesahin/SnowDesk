using System.Windows;
using SnowDesk.Engine;
using SnowDesk.Models;
using WpfButton = System.Windows.Controls.Button;

namespace SnowDesk.UI;

public partial class WardrobeWindow : Window
{
    private readonly SnowPhysicsEngine _engine;

    public WardrobeWindow(SnowPhysicsEngine engine)
    {
        InitializeComponent();
        _engine = engine;
    }

    private void Hat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string tag } && Enum.TryParse<SnowmanHatType>(tag, out var hat))
            _engine.ApplySnowmanAppearance(hat: hat);
    }

    private void Prop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string tag } && Enum.TryParse<SnowmanPropType>(tag, out var prop))
            _engine.ApplySnowmanAppearance(prop: prop);
    }

    private void Scarf_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string tag } && Enum.TryParse<SnowmanScarfTheme>(tag, out var scarf))
            _engine.ApplySnowmanAppearance(scarf: scarf);
    }
}
