using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace InplaceEditBoxLib.Views
{
    /// <summary>
    ///     An adorner class that contains a TextBox to provide editing capability
    ///     for an EditBox control. The editable TextBox resides in the
    ///     AdornerLayer. When the EditBox is in editing mode, the TextBox is given a size
    ///     it with desired size; otherwise, arrange it with size(0,0,0,0).
    ///     This code used part of ATC Avalon Team's work
    ///     (http://blogs.msdn.com/atc_avalon_team/archive/2006/03/14/550934.aspx)
    /// </summary>
    internal sealed class EditBoxAdorner : Adorner
    {
        #region constructor

        /// <summary>
        ///     Inialize the EditBoxAdorner.
        ///     +---> adorningElement (TextBox)
        ///     |
        ///     adornedElement (TextBlock)
        /// </summary>
        /// <param name="adornedElement"></param>
        /// <param name="adorningElement"></param>
        /// <param name="editBox"></param>
        public EditBoxAdorner(UIElement adornedElement,
            TextBox adorningElement,
            EditBox editBox)
            : base(adornedElement)
        {
            mTextBox = adorningElement;
            Debug.Assert(mTextBox != null, "No TextBox!");

            mVisualChildren = new VisualCollection(this);

            BuildTextBox(editBox);
        }

        #endregion constructor

        #region Properties

        /// <summary>
        ///     override property to return infomation about visual tree.
        /// </summary>
        protected override int VisualChildrenCount
        {
            get { return mVisualChildren.Count; }
        }

        #endregion Properties

        #region fields

        /// <summary>
        ///     Extra padding for the content when it is displayed in the TextBox
        /// </summary>
        private const double ExtraWidth = 15;

        /// <summary>
        ///     Visual children
        /// </summary>
        private readonly VisualCollection mVisualChildren;

        /// <summary>
        ///     Control that contains both Adorned control and Adorner.
        ///     This reference is required to compute the width of the
        ///     surrounding scrollviewer.
        /// </summary>
        private EditBox mEditBox;

        /// <summary>
        ///     The TextBox that this adorner covers.
        /// </summary>
        private readonly TextBox mTextBox;

        /// <summary>
        ///     Whether the EditBox is in editing mode which means the Adorner is visible.
        /// </summary>
        private bool mIsVisible;

        /// <summary>
        ///     Canvas that contains the TextBox that provides the ability for it to
        ///     display larger than the current size of the cell so that the entire
        ///     contents of the cell can be edited
        /// </summary>
        private Canvas mCanvas;

        /// <summary>
        ///     Maximum size of the textbox in dependents of the surrounding scrollviewer
        ///     is computed o demand in measure method and invalidated when visibility of Adorner changes.
        /// </summary>
        private double mTextBoxMaxWidth = double.PositiveInfinity;

        #endregion fields

        #region methods

        /// <summary>
        ///     Specifies whether a TextBox is visible
        ///     when the IsEditing property changes.
        /// </summary>
        /// <param name="isVisible"></param>
        public void UpdateVisibilty(bool isVisible)
        {
            mIsVisible = isVisible;
            InvalidateMeasure();
            mTextBoxMaxWidth = double.PositiveInfinity;
        }

        /// <summary>
        ///     override function to return infomation about visual tree.
        /// </summary>
        protected override Visual GetVisualChild(int index)
        {
            return mVisualChildren[index];
        }

        /// <summary>
        ///     override function to arrange elements.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (mIsVisible)
                mTextBox.Arrange(new Rect(-1, -1, finalSize.Width, finalSize.Height));
            else // if there is no editable mode, there is no need to show elements.
                mTextBox.Arrange(new Rect(0, 0, 0, 0));

            return finalSize;
        }

        /// <summary>
        ///     Override to measure elements.
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            mTextBox.IsEnabled = mIsVisible;

            // if in editing mode, measure the space the adorner element should cover.
            if (mIsVisible)
            {
                if (double.IsInfinity(mTextBoxMaxWidth))
                {
                    Point position = mTextBox.PointToScreen(new Point(0, 0)),
                        controlPosition = mEditBox.ParentScrollViewer.PointToScreen(new Point(0, 0));

                    position.X = Math.Abs(controlPosition.X - position.X);
                    position.Y = Math.Abs(controlPosition.Y - position.Y);

                    mTextBoxMaxWidth = mEditBox.ParentScrollViewer.ViewportWidth - position.X;
                }

                if (AdornedElement.Visibility == Visibility.Collapsed)
                    return new Size(mTextBoxMaxWidth, mTextBox.DesiredSize.Height);

                // 
                if (constraint.Width > mTextBoxMaxWidth)
                    constraint.Width = mTextBoxMaxWidth;

                AdornedElement.Measure(constraint);
                mTextBox.Measure(constraint);

                var desiredWidth = AdornedElement.DesiredSize.Width + ExtraWidth;

                // since the adorner is to cover the EditBox, it should return 
                // the AdornedElement.Width, the extra 15 is to make it more clear.
                if (desiredWidth < mTextBoxMaxWidth)
                {
                    return new Size(desiredWidth, mTextBox.DesiredSize.Height);
                }
                AdornedElement.Visibility = Visibility.Collapsed;

                return new Size(mTextBoxMaxWidth, mTextBox.DesiredSize.Height);
            }
            return new Size(0, 0);
        }

        /// <summary>
        ///     Inialize necessary properties and hook necessary events on TextBox,
        ///     then add it into tree.
        /// </summary>
        private void BuildTextBox(EditBox editBox)
        {
            mEditBox = editBox;

            mCanvas = new Canvas();
            mCanvas.Children.Add(mTextBox);
            mVisualChildren.Add(mCanvas);

            // Bind TextBox onto editBox control property Text
            var binding = new Binding("Text");
            binding.Source = editBox;
            binding.Mode = BindingMode.TwoWay;
            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            mTextBox.SetBinding(TextBox.TextProperty, binding);

            // Bind Text onto AdornedElement property Text
            binding = new Binding("Text");
            binding.Source = AdornedElement;
            binding.Mode = BindingMode.TwoWay;
            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            mTextBox.SetBinding(TextBox.TextProperty, binding);

            // try to get the text box focused when layout finishes.
            mTextBox.LayoutUpdated += OnTextBoxLayoutUpdated;
        }
         /// <summary>
        ///    When you perform a Click outside the Control, this block of code will be fired.
        /// </summary>
        private void HandleClickOutsideOfControl(object sender, MouseButtonEventArgs e)
        {
            /*oopsy whooopsy, Don't Panic ! I just noticed that the only way to trigger the 
            mouse events is to make them fire via built-in controls, such as buttons.*/
           
           	//Virtual Button that will be exploited to launch an imaginary "press" event, this control won't be rendered.            
            var b = new Button();
            b.Click += (o, args) =>
            {
                mTextBox.Focusable = false;
                mTextBox.ReleaseMouseCapture();
            };
			//This peer will be responsable for firing the "press"-like event          
            var peer =
                new ButtonAutomationPeer(b);
            var invokeProv =
                peer.GetPattern(PatternInterface.Invoke)
                    as IInvokeProvider;
            invokeProv.Invoke();
            mTextBox.Focusable = true;
        }

        /// <summary>
        ///     When Layout finish, if in editable mode, update focus status on TextBox.
        /// </summary>
        private void OnTextBoxLayoutUpdated(object sender, EventArgs e)
        {
        	//Enables <Focusable> again so you can implement the control again
            mTextBox.Focusable = true;
            if (mIsVisible)
                if (mTextBox.IsFocused == false)
                {
                	//Virtual Button that will be exploited to launch an imaginary "press" event, this control won't be rendered.
                    var b = new Button();
                    b.Click += (o, args) =>
                    {
                    	//This line of code captures the Mouse events
                        Mouse.Capture(mTextBox);
                        //You know... a handler?
                        AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                            new MouseButtonEventHandler(HandleClickOutsideOfControl), false);
                    };
                    //This peer will be responsable for firing the "press"-like event
                    var peer =
                        new ButtonAutomationPeer(b);
                    var invokeProv =
                        peer.GetPattern(PatternInterface.Invoke)
                            as IInvokeProvider;
                    invokeProv.Invoke();	
                    mTextBox.Focusable = false;
                }
        }

        #endregion methods
    }
}