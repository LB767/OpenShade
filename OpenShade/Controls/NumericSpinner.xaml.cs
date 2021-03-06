﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace OpenShade.Controls
{
    /// <summary>
    /// Interaction logic for NumericSpinner.xaml
    /// </summary>
    public partial class NumericSpinner : UserControl
    {
        #region Fields

        public event EventHandler PropertyChanged;
        public event EventHandler ValueChanged;

        #endregion

        public NumericSpinner()
        {
            InitializeComponent();

            tb_main.SetBinding(TextBox.TextProperty, new Binding("Value")
            {
                ElementName = "root_numeric_spinner",
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            DependencyPropertyDescriptor.FromProperty(ValueProperty, typeof(NumericSpinner)).AddValueChanged(this, PropertyChanged);
            DependencyPropertyDescriptor.FromProperty(ValueProperty, typeof(NumericSpinner)).AddValueChanged(this, ValueChanged);
            DependencyPropertyDescriptor.FromProperty(DecimalsProperty, typeof(NumericSpinner)).AddValueChanged(this, PropertyChanged);
            DependencyPropertyDescriptor.FromProperty(MinValueProperty, typeof(NumericSpinner)).AddValueChanged(this, PropertyChanged);
            DependencyPropertyDescriptor.FromProperty(MaxValueProperty, typeof(NumericSpinner)).AddValueChanged(this, PropertyChanged);

            PropertyChanged += (x, y) => validate();
        }

        #region ValueProperty

        public readonly static DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value",
            typeof(decimal),
            typeof(NumericSpinner),
            new PropertyMetadata(new decimal(0)));

        public decimal Value
        {
            get { return (decimal)GetValue(ValueProperty); }
            set
            {
                if (value < MinValue)
                    value = MinValue;
                if (value > MaxValue)
                    value = MaxValue;
                SetValue(ValueProperty, value);
            }
        }

        #endregion

        #region StepProperty

        public readonly static DependencyProperty StepProperty = DependencyProperty.Register(
            "Step",
            typeof(decimal),
            typeof(NumericSpinner),
            new PropertyMetadata(new decimal(0.1)));

        public decimal Step
        {
            get { return (decimal)GetValue(StepProperty); }
            set
            {
                SetValue(StepProperty, value);
            }
        }

        #endregion

        #region DecimalsProperty

        public readonly static DependencyProperty DecimalsProperty = DependencyProperty.Register(
            "Decimals",
            typeof(int),
            typeof(NumericSpinner),
            new PropertyMetadata(2));

        public int Decimals
        {
            get { return (int)GetValue(DecimalsProperty); }
            set
            {
                SetValue(DecimalsProperty, value);
            }
        }

        #endregion

        #region MinValueProperty

        public readonly static DependencyProperty MinValueProperty = DependencyProperty.Register(
            "MinValue",
            typeof(decimal),
            typeof(NumericSpinner),
            new PropertyMetadata(decimal.MinValue));

        public decimal MinValue
        {
            get { return (decimal)GetValue(MinValueProperty); }
            set
            {
                if (value > MaxValue)
                    MaxValue = value;
                SetValue(MinValueProperty, value);
            }
        }

        #endregion

        #region MaxValueProperty

        public readonly static DependencyProperty MaxValueProperty = DependencyProperty.Register(
            "MaxValue",
            typeof(decimal),
            typeof(NumericSpinner),
            new PropertyMetadata(decimal.MaxValue));

        public decimal MaxValue
        {
            get { return (decimal)GetValue(MaxValueProperty); }
            set
            {
                if (value < MinValue)
                    value = MinValue;
                SetValue(MaxValueProperty, value);
            }
        }

        #endregion

        /// <summary>
        /// Revalidate the object, whenever a value is changed...
        /// </summary>
        private void validate()
        {
            // Logically, This is not needed at all... as it's handled within other properties...
            if (MinValue > MaxValue) MinValue = MaxValue;
            if (MaxValue < MinValue) MaxValue = MinValue;
            if (Value < MinValue) Value = MinValue;
            if (Value > MaxValue) Value = MaxValue;

            Value = decimal.Round(Value, Decimals);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void cmdUp_Click(object sender, RoutedEventArgs e)
        {
            Value += Step;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void cmdDown_Click(object sender, RoutedEventArgs e)
        {
            Value -= Step;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void tb_main_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^([0-9]|\\.)\\.?[0-9]*");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private void tb_main_LostFocus(object sender, RoutedEventArgs e)
        {
            double test;
            if (double.TryParse(tb_main.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out test))
            {
                validate();
            }
            else
            {
                tb_main.Text = MinValue.ToString();
            }
        }

    }
}
