using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ZenTimings.Controls
{
    /// <summary>
    /// A label and its value as one element, so a timings panel is a single list of rows that can be
    /// reordered freely instead of two parallel label/value columns that must stay in step.
    /// <para>
    /// Rows line up within the nearest ancestor with <c>Grid.IsSharedSizeScope="True"</c>. Setting
    /// <see cref="UIElement.IsEnabled"/> greys out the whole row, label and value together, e.g. when
    /// the value isn't available.
    /// </para>
    /// </summary>
    public partial class TimingRow : UserControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(TimingRow), new PropertyMetadata(null, OnVoltageStateChanged, CoerceDisplayedValue));

        public static readonly DependencyProperty AllowManualVoltageProperty = DependencyProperty.Register(
            nameof(AllowManualVoltage), typeof(bool), typeof(TimingRow), new PropertyMetadata(false, OnVoltageStateChanged));

        private string manualVoltage;

        public static readonly DependencyProperty LabelToolTipProperty = DependencyProperty.Register(
            nameof(LabelToolTip), typeof(object), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty ValueToolTipProperty = DependencyProperty.Register(
            nameof(ValueToolTip), typeof(object), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty ValueMinWidthProperty = DependencyProperty.Register(
            nameof(ValueMinWidth), typeof(double), typeof(TimingRow), new PropertyMetadata(0.0, OnValueMinWidthChanged));

        public TimingRow()
        {
            InitializeComponent();
            ValueColumn.MinWidth = ValueMinWidth;
            UpdateVoltageState();
        }

        /// <summary>Allows a voltage to be overridden for display without changing the sensor.</summary>
        public bool AllowManualVoltage
        {
            get => (bool)GetValue(AllowManualVoltageProperty);
            set => SetValue(AllowManualVoltageProperty, value);
        }

        // Coercion keeps both the original binding and the latest sensor value intact during refreshes.
        private static object CoerceDisplayedValue(DependencyObject d, object baseValue)
        {
            return ((TimingRow)d).manualVoltage ?? baseValue;
        }

        private static void OnVoltageStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TimingRow)d).UpdateVoltageState();
        }

        private bool CanEditVoltage => AllowManualVoltage;

        private void UpdateVoltageState()
        {
            if (ValueBlock == null || !AllowManualVoltage)
                return;

            LabelBlock.IsEnabled = Value != "N/A";
            ValueBlock.Cursor = CanEditVoltage ? Cursors.Hand : null;
            ValueBlock.SetCurrentValue(ToolTipProperty, manualVoltage != null
                ? "Manually entered voltage. Click to edit; clear to reset."
                : CanEditVoltage ? "Click to enter voltage manually." : ValueToolTip);
        }

        private void ValueBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!CanEditVoltage)
                return;

            VoltageEditor.Text = Value == "N/A" ? "" : Value?.TrimEnd('V', 'v').Trim() ?? "";
            VoltageEditor.ToolTip = "Enter voltage in volts. Clear and press Enter to restore the sensor value.";
            VoltageEditor.ClearValue(BorderBrushProperty);
            VoltageEditor.Height = ValueBlock.ActualHeight;
            ValueBlock.Visibility = Visibility.Hidden;
            VoltageEditor.Visibility = Visibility.Visible;
            VoltageEditor.Focus();
            VoltageEditor.SelectAll();
            e.Handled = true;
        }

        private void VoltageEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                EndVoltageEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                SaveVoltageEdit();
                e.Handled = true;
            }
        }

        private void VoltageEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (VoltageEditor.IsVisible)
                EndVoltageEdit();
        }

        private void SaveVoltageEdit()
        {
            string text = VoltageEditor.Text.Trim().TrimEnd('V', 'v').Trim().Replace(',', '.');
            if (text.Length == 0)
                manualVoltage = null;
            else if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal voltage)
                && voltage >= 0.0001m && voltage <= 10)
                manualVoltage = voltage.ToString("F4", CultureInfo.CurrentCulture) + "V";
            else
            {
                VoltageEditor.ToolTip = "Enter a voltage from 0.0001 to 10 V (e.g. 1.35).";
                VoltageEditor.BorderBrush = Brushes.Red;
                VoltageEditor.SelectAll();
                return;
            }

            CoerceValue(ValueProperty);
            UpdateVoltageState();
            EndVoltageEdit();
        }

        private void EndVoltageEdit()
        {
            VoltageEditor.Visibility = Visibility.Collapsed;
            ValueBlock.Visibility = Visibility.Visible;
        }

        /// <summary>Row caption. A string so bindings to it may use StringFormat.</summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>Displayed value. A string so bindings to it may use StringFormat; "N/A" renders greyed out.</summary>
        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public object LabelToolTip
        {
            get => GetValue(LabelToolTipProperty);
            set => SetValue(LabelToolTipProperty, value);
        }

        public object ValueToolTip
        {
            get => GetValue(ValueToolTipProperty);
            set => SetValue(ValueToolTipProperty, value);
        }

        /// <summary>Minimum width of the value column, e.g. set for a whole group through an implicit style.</summary>
        public double ValueMinWidth
        {
            get => (double)GetValue(ValueMinWidthProperty);
            set => SetValue(ValueMinWidthProperty, value);
        }

        // A ColumnDefinition isn't in the visual tree, so it can't reach the row through RelativeSource.
        private static void OnValueMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimingRow row = (TimingRow)d;
            if (row.ValueColumn != null)
                row.ValueColumn.MinWidth = (double)e.NewValue;
        }

        /// <summary>The TextBlock that renders <see cref="Value"/>, for callers that measure or recolour it.</summary>
        public TextBlock ValueTextBlock => ValueBlock;

        /// <summary>
        /// Path of the binding behind the text <paramref name="text"/> shows. For a row's value block that is
        /// the row's own <see cref="Value"/> binding (e.g. "Timings.CL"), not the internal link to the row;
        /// for any other TextBlock it is its Text binding. Null when unbound.
        /// </summary>
        public static string GetDisplayedBindingPath(TextBlock text)
        {
            if (text == null)
                return null;

            for (DependencyObject parent = VisualTreeHelper.GetParent(text); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is TimingRow row)
                {
                    return ReferenceEquals(row.ValueBlock, text)
                        ? BindingOperations.GetBinding(row, ValueProperty)?.Path?.Path
                        : null;
                }
            }

            return BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path?.Path;
        }
    }
}
