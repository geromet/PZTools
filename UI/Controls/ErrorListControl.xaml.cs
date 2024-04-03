﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DataInput;
using DataInput.Models;

namespace UI.Controls
{
    public partial class ErrorListControl : UserControl
    {
        public static readonly DependencyProperty ErrorsProperty =
            DependencyProperty.Register("Errors", typeof(List<Error>), typeof(ErrorListControl), new PropertyMetadata(null));

        public List<Error> Errors
        {
            get { return (List<Error>)GetValue(ErrorsProperty); }
            set { SetValue(ErrorsProperty, value); }
        }

        public ErrorListControl()
        {
            InitializeComponent();
            DataContext = Errors;
        }
    }

}
    