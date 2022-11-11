using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UBot.Controls
{
    public class NumericValidationBehavior : Behavior<Entry>
    {
        protected override void OnAttachedTo(Entry entry)
        {
            entry.TextChanged += OnEntryTextChanged;
            base.OnAttachedTo(entry);
        }

        protected override void OnDetachingFrom(Entry entry)
        {
            entry.TextChanged -= OnEntryTextChanged;
            base.OnDetachingFrom(entry);
        }

        private static void OnEntryTextChanged(object sender, TextChangedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.NewTextValue))
            {
                bool isValid = args.NewTextValue.ToCharArray().All(x => char.IsDigit(x) || x.Equals(','));
                object completeNumber = 0;

                if (isValid)
                    if (args.NewTextValue[args.NewTextValue.Length - 1] == ',')
                        completeNumber = args.NewTextValue;
                    else if (int.TryParse(args.NewTextValue, out _))
                        completeNumber = int.Parse(args.NewTextValue);
                    else if (float.TryParse(args.NewTextValue, out _))
                        completeNumber = float.Parse(args.NewTextValue);

                ((Entry)sender).Text = isValid ? completeNumber.ToString() : args.NewTextValue.Remove(args.NewTextValue.Length - 1);
            }
        }
    }
}
