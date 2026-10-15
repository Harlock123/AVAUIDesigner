using AVAUIDesigner.Controls;
using AVAUIDesigner.Engine.Descriptors;
using AVAUIDesigner.Engine.Document;
using AVAUIDesigner.Engine.Serialization;

namespace AVAUIDesigner.Engine.Tests;

/// <summary>Shared fixtures: the Appendix A samples and a populated registry.</summary>
public static class Samples
{
    private static readonly Lazy<ControlDescriptorRegistry> LazyRegistry = new(StandardControlDescriptors.CreateRegistry);

    public static ControlDescriptorRegistry Registry => LazyRegistry.Value;

    public const string MainWindow = """
        <Window xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                mc:Ignorable="d"
                x:Class="MyApp.Views.MainWindow"
                Title="MainWindow"
                Width="800"
                Height="450">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>

                <TextBlock x:Name="lblTitle"
                           Grid.Row="0"
                           Text="Welcome"
                           FontSize="24"
                           HorizontalAlignment="Center"
                           Margin="0,16,0,8" />

                <TextBox x:Name="txtName"
                         Grid.Row="1"
                         Watermark="Enter your name"
                         Margin="16"
                         VerticalAlignment="Center"
                         TextChanged="TxtName_TextChanged" />

                <Button x:Name="btnOk"
                        Grid.Row="2"
                        Content="OK"
                        Width="75"
                        HorizontalAlignment="Right"
                        Margin="16"
                        Click="BtnOk_Click" />
            </Grid>
        </Window>
        """;

    public const string MvvmMainWindow = """
        <Window xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:vm="clr-namespace:MyApp.ViewModels"
                x:Class="MyApp.Views.MainWindow"
                x:DataType="vm:MainWindowViewModel"
                Title="MainWindow"
                Width="800"
                Height="450">
            <Window.DataContext>
                <vm:MainWindowViewModel />
            </Window.DataContext>
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0"
                           Text="Welcome"
                           FontSize="24"
                           HorizontalAlignment="Center"
                           Margin="0,16,0,8" />

                <TextBox Grid.Row="1"
                         Text="{Binding TxtName, Mode=TwoWay}"
                         Watermark="Enter your name"
                         Margin="16"
                         VerticalAlignment="Center" />

                <Button Grid.Row="2"
                        Content="OK"
                        Width="75"
                        HorizontalAlignment="Right"
                        Margin="16"
                        Command="{Binding BtnOkCommand}" />
            </Grid>
        </Window>
        """;

    public static DesignDocument Parse(string xaml) => new XamlParser().Parse(xaml, Registry);

    public static string Serialize(DesignDocument doc) => new XamlSerializer().Serialize(doc);

    /// <summary>Wraps body XAML in a Window with the default namespaces.</summary>
    public static DesignDocument ParseInWindow(string body) => Parse($"""
        <Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
        {body}
        </Window>
        """);
}
