using System;
using System.Collections.Generic;
using System.Windows;

namespace OpenShade.Classes
{
    // This makes it possible to base styles on a dynamic resource (ie a base theme) that is changing at runtime
    // In other words, it's the equivalent of BasedOn="{DynamicResource styleKey}" but it works

    public class DynamicStyle
    {
        public static Style GetBaseStyle(DependencyObject obj)
        {
            return (Style)obj.GetValue(BaseStyleProperty);
        }

        public static void SetBaseStyle(DependencyObject obj, Style value)
        {
            obj.SetValue(BaseStyleProperty, value);
        }

        // Using a DependencyProperty as the backing store for BaseStyle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BaseStyleProperty =
            DependencyProperty.RegisterAttached("BaseStyle", typeof(Style), typeof(DynamicStyle), new UIPropertyMetadata(DynamicStyle.StylesChanged));

        public static Style GetDerivedStyle(DependencyObject obj)
        {
            return (Style)obj.GetValue(DerivedStyleProperty);
        }

        public static void SetDerivedStyle(DependencyObject obj, Style value)
        {
            obj.SetValue(DerivedStyleProperty, value);
        }

        // Using a DependencyProperty as the backing store for DerivedStyle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DerivedStyleProperty =
            DependencyProperty.RegisterAttached("DerivedStyle", typeof(Style), typeof(DynamicStyle), new UIPropertyMetadata(DynamicStyle.StylesChanged));

        private static void StylesChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (!typeof(FrameworkElement).IsAssignableFrom(target.GetType()))
                throw new InvalidCastException("Target must be FrameworkElement");

            var Element = (FrameworkElement)target;

            var Styles = new List<Style>();

            var BaseStyle = GetBaseStyle(target);

            if (BaseStyle != null)
                Styles.Add(BaseStyle);

            var DerivedStyle = GetDerivedStyle(target);

            if (DerivedStyle != null)
                Styles.Add(DerivedStyle);

            Element.Style = MergeStyles(Styles);
        }

        private static Style MergeStyles(ICollection<Style> Styles)
        {
            var NewStyle = new Style();

            foreach (var Style in Styles)
            {
                foreach (var Setter in Style.Setters)
                    NewStyle.Setters.Add(Setter);

                foreach (var Trigger in Style.Triggers)
                    NewStyle.Triggers.Add(Trigger);
            }

            return NewStyle;
        }
    }
}
