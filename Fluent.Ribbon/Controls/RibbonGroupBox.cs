// ReSharper disable once CheckNamespace
namespace Fluent;

using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Fluent.Extensions;
using Fluent.Helpers;
using Fluent.Internal;
using Fluent.Internal.KnownBoxes;

/// <summary>
/// RibbonGroup represents a logical group of controls as they appear on
/// a RibbonTab.  These groups can resize its content
/// </summary>
[TemplatePart(Name = "PART_DialogLauncherButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_HeaderContentControl", Type = typeof(ContentControl))]
[TemplatePart(Name = "PART_CollapsedHeaderContentControl", Type = typeof(ContentControl))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_UpPanel", Type = typeof(Panel))]
[TemplatePart(Name = "PART_ParentPanel", Type = typeof(Panel))]
[TemplatePart(Name = "PART_SnappedImage", Type = typeof(Image))]
[System.Diagnostics.DebuggerDisplay("class{GetType().FullName}: Header = {Header}, Items.Count = {Items.Count}, State = {State}, IsSimplified = {IsSimplified}")]
public class RibbonGroupBox : HeaderedItemsControl, IQuickAccessItemProvider, IDropDownControl, IKeyTipedControl, IHeaderedControl, ILogicalChildSupport, IMediumIconProvider, ISimplifiedStateControl, ILargeIconProvider
{
    #region Fields

    // up part
    private Panel? upPanel;

    private Panel? parentPanel;

    // Freezed image (created during snapping)
    private Image? snappedImage;

    // Is visual currently snapped
    private bool isSnapped;

    private readonly ItemContainerGeneratorAction updateChildSizesItemContainerGeneratorAction;

    #endregion

    #region Properties

    /// <summary>
    /// Get the <see cref="ContentControl"/> responsible for rendering the header.
    /// </summary>
    public ContentControl? HeaderContentControl { get; private set; }

    /// <summary>
    /// Get the <see cref="ContentControl"/> responsible for rendering the header when <see cref="State"/> is equal to <see cref="RibbonGroupBoxState.Collapsed"/>.
    /// </summary>
    public ContentControl? CollapsedHeaderContentControl { get; private set; }

    #region KeyTip

    /// <inheritdoc />
    public string? KeyTip
    {
        get { return (string?)this.GetValue(KeyTipProperty); }
        set { this.SetValue(KeyTipProperty, value); }
    }

    /// <summary>
    /// Using a DependencyProperty as the backing store for Keys.
    /// This enables animation, styling, binding, etc...
    /// </summary>
    public static readonly DependencyProperty KeyTipProperty = Fluent.KeyTip.KeysProperty.AddOwner(typeof(RibbonGroupBox));

    #endregion

    #region Header-Options

    /// <summary>
    /// <see cref="DependencyProperty"/> for IsCollapsedHeaderContentPresenter.
    /// </summary>
    public static readonly DependencyProperty IsCollapsedHeaderContentPresenterProperty = DependencyProperty.RegisterAttached("IsCollapsedHeaderContentPresenter", typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.FalseBox));

    /// <summary>
    /// Sets the value of <see cref="IsCollapsedHeaderContentPresenterProperty"/>.
    /// </summary>
    public static void SetIsCollapsedHeaderContentPresenter(DependencyObject element, bool value)
    {
        element.SetValue(IsCollapsedHeaderContentPresenterProperty, BooleanBoxes.Box(value));
    }

    /// <summary>
    /// Gets the value of <see cref="IsCollapsedHeaderContentPresenterProperty"/>.
    /// </summary>
    [AttachedPropertyBrowsableForType(typeof(RibbonGroupBox))]
    public static bool GetIsCollapsedHeaderContentPresenter(DependencyObject element)
    {
        return (bool)element.GetValue(IsCollapsedHeaderContentPresenterProperty);
    }

    #endregion

    /// <inheritdoc />
    public Popup? DropDownPopup { get; private set; }

    /// <inheritdoc />
    public bool IsContextMenuOpened { get; set; }

    /// <summary>
    /// Shorthand for whether the GroupBox is in a state where it can act as a button
    /// </summary>
    public bool IsInButtonState => this.State == RibbonGroupBoxState.Collapsed || this.State == RibbonGroupBoxState.QuickAccess;

    #region StateDefinition

    /// <summary>
    /// Gets or sets the state transition for full mode
    /// </summary>
    public RibbonGroupBoxStateDefinition StateDefinition
    {
        get { return (RibbonGroupBoxStateDefinition)this.GetValue(StateDefinitionProperty); }
        set { this.SetValue(StateDefinitionProperty, value); }
    }

    /// <summary>Identifies the <see cref="StateDefinition"/> dependency property.</summary>
    public static readonly DependencyProperty StateDefinitionProperty =
        DependencyProperty.Register(nameof(StateDefinition), typeof(RibbonGroupBoxStateDefinition), typeof(RibbonGroupBox),
            new PropertyMetadata(new RibbonGroupBoxStateDefinition(null), OnStateDefinitionChanged));

    // Handles StateDefinitionProperty changes
    internal static void OnStateDefinitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (RibbonGroupBox)d;
        if (!box.IsSimplified)
        {
            box.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
        }
    }

    #endregion

    #region SimplifiedStateDefinition

    /// <summary>
    /// Gets or sets the state transition for simplified mode
    /// </summary>
    public RibbonGroupBoxStateDefinition SimplifiedStateDefinition
    {
        get { return (RibbonGroupBoxStateDefinition)this.GetValue(SimplifiedStateDefinitionProperty); }
        set { this.SetValue(SimplifiedStateDefinitionProperty, value); }
    }

    /// <summary>Identifies the <see cref="SimplifiedStateDefinition"/> dependency property.</summary>
    public static readonly DependencyProperty SimplifiedStateDefinitionProperty =
        DependencyProperty.Register(nameof(SimplifiedStateDefinition), typeof(RibbonGroupBoxStateDefinition), typeof(RibbonGroupBox),
            new PropertyMetadata(new RibbonGroupBoxStateDefinition("Large,Middle,Collapsed"), OnSimplifiedStateDefinitionChanged));

    // Handles SimplifiedStateDefinitionProperty changes
    internal static void OnSimplifiedStateDefinitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (RibbonGroupBox)d;
        if (box.IsSimplified)
        {
            box.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
        }
    }

    #endregion

    #region State

    /// <summary>
    /// Gets or sets the current state of the group
    /// </summary>
    public RibbonGroupBoxState State
    {
        get { return (RibbonGroupBoxState)this.GetValue(StateProperty); }
        set { this.SetValue(StateProperty, value); }
    }

    /// <summary>Identifies the <see cref="State"/> dependency property.</summary>
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(RibbonGroupBoxState), typeof(RibbonGroupBox), new PropertyMetadata(RibbonGroupBoxState.Large, OnStateChanged));

    /// <summary>
    /// On state property changed
    /// </summary>
    /// <param name="d">Object</param>
    /// <param name="e">The event data</param>
    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ribbonGroupBox = (RibbonGroupBox)d;
        ribbonGroupBox.updateChildSizesItemContainerGeneratorAction.QueueAction();
        ribbonGroupBox.Focusable = ribbonGroupBox.IsInButtonState;
    }

    private void UpdateChildSizes()
    {
        var groupBoxState = this.State is RibbonGroupBoxState.QuickAccess
            ? RibbonGroupBoxState.Collapsed
            : this.State;
        var isSimplified = this.IsSimplified;

        foreach (var item in this.Items)
        {
            var element = this.ItemContainerGenerator.ContainerFromItem(item);
            this.UpdateChildSizesOfUIElement(element, groupBoxState, isSimplified);
        }
    }

    private void UpdateChildSizesOfUIElement(DependencyObject? element, RibbonGroupBoxState groupBoxState, bool isSimplified)
    {
        if (element is null)
        {
            return;
        }

        if (element is Panel panel)
        {
            for (var i = 0; i < panel.Children.Count; i++)
            {
                this.UpdateChildSizesOfUIElement(panel.Children[i], groupBoxState, isSimplified);
            }
        }

        if (element is ContentPresenter)
        {
            element = UIHelper.GetFirstVisualChild(element) ?? element;
        }

        UpdateIsSimplifiedOfUIElement(element, isSimplified);
        RibbonProperties.SetAppropriateSize(element, groupBoxState, isSimplified);
    }

    #endregion

    #region Scale

    // Current scale index
    private int scale;

    /// <summary>
    /// Gets or sets scale index (for internal IRibbonScalableControl)
    /// </summary>
    internal int Scale
    {
        get { return this.scale; }

        set
        {
            var difference = value - this.scale;
            this.scale = value;

            for (var i = 0; i < Math.Abs(difference); i++)
            {
                if (difference > 0)
                {
                    this.EnlargeScalableItems();
                }
                else
                {
                    this.ReduceScalableItems();
                }
            }
        }
    }

    private enum ScaleDirection
    {
        Enlarge,
        Reduce
    }

    // Finds and increases size of all scalable elements in this group box
    private void EnlargeScalableItems()
    {
        this.ScaleScaleableItems(ScaleDirection.Enlarge);
    }

    // Finds and decreases size of all scalable elements in this group box
    private void ReduceScalableItems()
    {
        this.ScaleScaleableItems(ScaleDirection.Reduce);
    }

    private void ScaleScaleableItems(ScaleDirection scaleDirection)
    {
        foreach (var item in this.Items)
        {
            var scalableRibbonControl = this.ItemContainerGenerator.ContainerOrContainerContentFromItem<IScalableRibbonControl>(item);

            if (scalableRibbonControl is null
                || (scalableRibbonControl is UIElement uiElement && uiElement.Visibility != Visibility.Visible))
            {
                continue;
            }

            switch (scaleDirection)
            {
                case ScaleDirection.Enlarge:
                    scalableRibbonControl.Enlarge();
                    break;

                case ScaleDirection.Reduce:
                    scalableRibbonControl.Reduce();
                    break;
            }
        }
    }

    private void ResetScaleableItems()
    {
        foreach (var item in this.Items)
        {
            var scalableRibbonControl = this.ItemContainerGenerator.ContainerOrContainerContentFromItem<IScalableRibbonControl>(item);

            if (scalableRibbonControl is null
                || (scalableRibbonControl is UIElement uiElement && uiElement.Visibility != Visibility.Visible))
            {
                continue;
            }

            scalableRibbonControl.ResetScale();
        }
    }

    /// <summary>
    /// Gets or sets whether to reset cache when scalable control is scaled
    /// </summary>
    internal ScopeGuard CacheResetGuard { get; }

    #endregion

    #region IsLauncherVisible

    /// <summary>
    /// Gets or sets dialog launcher button visibility
    /// </summary>
    public bool IsLauncherVisible
    {
        get { return (bool)this.GetValue(IsLauncherVisibleProperty); }
        set { this.SetValue(IsLauncherVisibleProperty, BooleanBoxes.Box(value)); }
    }

    /// <summary>Identifies the <see cref="IsLauncherVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsLauncherVisibleProperty =
        DependencyProperty.Register(nameof(IsLauncherVisible), typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.FalseBox));

    #endregion

    #region LauncherKeys

    /// <summary>
    /// Gets or sets key tip for dialog launcher button
    /// </summary>
    [DisplayName("DialogLauncher Keys")]
    [Category("KeyTips")]
    [Description("Key tip keys for dialog launcher button")]
    public string? LauncherKeys
    {
        get { return (string?)this.GetValue(LauncherKeysProperty); }
        set { this.SetValue(LauncherKeysProperty, value); }
    }

    /// <summary>Identifies the <see cref="LauncherKeys"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherKeysProperty =
        DependencyProperty.Register(nameof(LauncherKeys),
            typeof(string), typeof(RibbonGroupBox), new PropertyMetadata(OnLauncherKeysChanged));

    private static void OnLauncherKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ribbonGroupBox = (RibbonGroupBox)d;
        if (ribbonGroupBox.LauncherButton is not null)
        {
            ribbonGroupBox.LauncherButton.KeyTip = (string?)e.NewValue;
        }
    }

    #endregion

    #region LauncherIcon

    /// <summary>
    /// Gets or sets launcher button icon
    /// </summary>
    public object? LauncherIcon
    {
        get { return this.GetValue(LauncherIconProperty); }
        set { this.SetValue(LauncherIconProperty, value); }
    }

    /// <summary>Identifies the <see cref="LauncherIcon"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherIconProperty =
        DependencyProperty.Register(nameof(LauncherIcon), typeof(object), typeof(RibbonGroupBox), new PropertyMetadata(LogicalChildSupportHelper.OnLogicalChildPropertyChanged));

    #endregion

    #region LauncherText

    /// <summary>
    /// Gets or sets launcher button text
    /// </summary>
    public string? LauncherText
    {
        get { return (string?)this.GetValue(LauncherTextProperty); }
        set { this.SetValue(LauncherTextProperty, value); }
    }

    /// <summary>Identifies the <see cref="LauncherText"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherTextProperty =
        DependencyProperty.Register(nameof(LauncherText), typeof(string), typeof(RibbonGroupBox), new PropertyMetadata());

    #endregion

    #region LauncherCommand

    /// <summary>
    /// Gets or sets the command to invoke when this button is pressed. This is a dependency property.
    /// </summary>
    [Category("Action")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    [Bindable(true)]
    public ICommand? LauncherCommand
    {
        get
        {
            return (ICommand?)this.GetValue(LauncherCommandProperty);
        }

        set
        {
            this.SetValue(LauncherCommandProperty, value);
        }
    }

    /// <summary>
    /// Gets or sets the parameter to pass to the System.Windows.Controls.Primitives.ButtonBase.Command property. This is a dependency property.
    /// </summary>
    [Bindable(true)]
    [Localizability(LocalizationCategory.NeverLocalize)]
    [Category("Action")]
    public object? LauncherCommandParameter
    {
        get
        {
            return this.GetValue(LauncherCommandParameterProperty);
        }

        set
        {
            this.SetValue(LauncherCommandParameterProperty, value);
        }
    }

    /// <summary>
    /// Gets or sets the element on which to raise the specified command. This is a dependency property.
    /// </summary>
    [Bindable(true)]
    [Category("Action")]
    public IInputElement? LauncherCommandTarget
    {
        get
        {
            return (IInputElement?)this.GetValue(LauncherCommandTargetProperty);
        }

        set
        {
            this.SetValue(LauncherCommandTargetProperty, value);
        }
    }

    /// <summary>Identifies the <see cref="LauncherCommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherCommandParameterProperty = DependencyProperty.Register(nameof(LauncherCommandParameter), typeof(object), typeof(RibbonGroupBox), new PropertyMetadata());

    /// <summary>Identifies the <see cref="LauncherCommand"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherCommandProperty = DependencyProperty.Register(nameof(LauncherCommand), typeof(ICommand), typeof(RibbonGroupBox), new PropertyMetadata());

    /// <summary>Identifies the <see cref="LauncherCommandTarget"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherCommandTargetProperty = DependencyProperty.Register(nameof(LauncherCommandTarget), typeof(IInputElement), typeof(RibbonGroupBox), new PropertyMetadata());

    #endregion

    #region LauncherToolTip

    /// <summary>
    /// Gets or sets launcher button tooltip
    /// </summary>
    public object? LauncherToolTip
    {
        get { return this.GetValue(LauncherToolTipProperty); }
        set { this.SetValue(LauncherToolTipProperty, value); }
    }

    /// <summary>Identifies the <see cref="LauncherToolTip"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherToolTipProperty =
        DependencyProperty.Register(nameof(LauncherToolTip), typeof(object), typeof(RibbonGroupBox), new PropertyMetadata());

    #endregion

    #region IsLauncherEnabled

    /// <summary>
    /// Gets or sets whether launcher button is enabled
    /// </summary>
    public bool IsLauncherEnabled
    {
        get { return (bool)this.GetValue(IsLauncherEnabledProperty); }
        set { this.SetValue(IsLauncherEnabledProperty, BooleanBoxes.Box(value)); }
    }

    /// <summary>Identifies the <see cref="IsLauncherEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsLauncherEnabledProperty =
        DependencyProperty.Register(nameof(IsLauncherEnabled), typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.TrueBox));

    #endregion

    #region LauncherButton

    /// <summary>
    /// Gets launcher button
    /// </summary>
    public Button? LauncherButton
    {
        get { return (Button?)this.GetValue(LauncherButtonProperty); }
        private set { this.SetValue(LauncherButtonPropertyKey, value); }
    }

    // ReSharper disable once InconsistentNaming
    private static readonly DependencyPropertyKey LauncherButtonPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(LauncherButton), typeof(Button), typeof(RibbonGroupBox), new PropertyMetadata());

    /// <summary>Identifies the <see cref="LauncherButton"/> dependency property.</summary>
    public static readonly DependencyProperty LauncherButtonProperty = LauncherButtonPropertyKey.DependencyProperty;

    #endregion

    #region IsOpen

    /// <inheritdoc />
    public bool IsDropDownOpen
    {
        get { return (bool)this.GetValue(IsDropDownOpenProperty); }
        set { this.SetValue(IsDropDownOpenProperty, BooleanBoxes.Box(value)); }
    }

    /// <summary>Identifies the <see cref="IsDropDownOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.FalseBox, OnIsDropDownOpenChanged, CoerceIsDropDownOpen));

    private static object? CoerceIsDropDownOpen(DependencyObject d, object? basevalue)
    {
        var box = (RibbonGroupBox)d;

        if ((box.State != RibbonGroupBoxState.Collapsed)
            && (box.State != RibbonGroupBoxState.QuickAccess))
        {
            return BooleanBoxes.Box(false);
        }

        return basevalue;
    }

    #endregion

    #region LogicalChildren

    #region Icon

    /// <summary>
    /// Gets or sets icon
    /// </summary>
    public object? Icon
    {
        get { return this.GetValue(IconProperty); }
        set { this.SetValue(IconProperty, value); }
    }

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = RibbonControl.IconProperty.AddOwner(typeof(RibbonGroupBox), new PropertyMetadata(LogicalChildSupportHelper.OnLogicalChildPropertyChanged));

    #endregion

    #region MediumIcon

    /// <inheritdoc />
    public object? MediumIcon
    {
        get { return this.GetValue(MediumIconProperty); }
        set { this.SetValue(MediumIconProperty, value); }
    }

    /// <summary>Identifies the <see cref="MediumIcon"/> dependency property.</summary>
    public static readonly DependencyProperty MediumIconProperty = MediumIconProviderProperties.MediumIconProperty.AddOwner(typeof(RibbonGroupBox), new PropertyMetadata(LogicalChildSupportHelper.OnLogicalChildPropertyChanged));

    #endregion

    #region LargeIcon

    /// <inheritdoc />
    public object? LargeIcon
    {
        get { return this.GetValue(LargeIconProperty); }
        set { this.SetValue(LargeIconProperty, value); }
    }

    /// <summary>Identifies the <see cref="LargeIcon"/> dependency property.</summary>
    public static readonly DependencyProperty LargeIconProperty = LargeIconProviderProperties.LargeIconProperty.AddOwner(typeof(RibbonGroupBox), new PropertyMetadata(LogicalChildSupportHelper.OnLogicalChildPropertyChanged));

    #endregion
    
    #region IsSeparatorVisible

    /// <summary>
    /// Gets or sets whether the groupbox shows a separator.
    /// </summary>
    public bool IsSeparatorVisible
    {
        get { return (bool)this.GetValue(IsSeparatorVisibleProperty); }
        set { this.SetValue(IsSeparatorVisibleProperty, BooleanBoxes.Box(value)); }
    }

    /// <summary>Identifies the <see cref="IsSeparatorVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsSeparatorVisibleProperty =
        DependencyProperty.Register(nameof(IsSeparatorVisible), typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.TrueBox));

    #endregion

    #endregion

    #region IsSimplified

    /// <summary>
    /// Gets or sets whether or not the ribbon is in Simplified mode
    /// </summary>
    public bool IsSimplified
    {
        get { return (bool)this.GetValue(IsSimplifiedProperty); }
        private set { this.SetValue(IsSimplifiedPropertyKey, BooleanBoxes.Box(value)); }
    }

    // ReSharper disable once InconsistentNaming
    private static readonly DependencyPropertyKey IsSimplifiedPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsSimplified), typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.FalseBox, OnIsSimplifiedChanged));

    /// <summary>Identifies the <see cref="IsSimplified"/> dependency property.</summary>
    public static readonly DependencyProperty IsSimplifiedProperty = IsSimplifiedPropertyKey.DependencyProperty;

    /// <summary>
    /// Called when <see cref="IsSimplified"/> changes.
    /// </summary>
    private static void OnIsSimplifiedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (RibbonGroupBox)d;
        box.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
        box.updateChildSizesItemContainerGeneratorAction.QueueAction();
    }

    private static void UpdateIsSimplifiedOfUIElement(DependencyObject? element, bool isSimplified)
    {
        if (element is ISimplifiedStateControl simplifiedStateControl)
        {
            simplifiedStateControl.UpdateSimplifiedState(isSimplified);
        }
    }

    #endregion

    #endregion Properties

    #region Events

    /// <summary>
    /// Dialog launcher btton click event
    /// </summary>
    public event RoutedEventHandler? LauncherClick;

    /// <inheritdoc />
    public event EventHandler? DropDownOpened;

    /// <inheritdoc />
    public event EventHandler? DropDownClosed;

    #endregion

    #region Initialize

    /// <summary>
    /// Initializes static members of the <see cref="RibbonGroupBox"/> class.
    /// </summary>
    static RibbonGroupBox()
    {
        var type = typeof(RibbonGroupBox);

        DefaultStyleKeyProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(type));
        VisibilityProperty.AddOwner(type, new PropertyMetadata(OnVisibilityChanged));
        FontSizeProperty.AddOwner(type, new FrameworkPropertyMetadata(OnFontSizeChanged));
        FontFamilyProperty.AddOwner(type, new FrameworkPropertyMetadata(OnFontFamilyChanged));

        PopupService.Attach(type);
        ContextMenuService.Attach(type);
    }

    private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (RibbonGroupBox)d;
        box.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
    }

    private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (RibbonGroupBox)d;
        box.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
    }

    private static void OnFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (RibbonGroupBox)d;
        box.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public RibbonGroupBox()
    {
        this.CacheResetGuard = new ScopeGuard(() => { }, () => { });

        this.CoerceValue(ContextMenuProperty);
        this.Focusable = false;

        this.Loaded += this.OnLoaded;
        this.Unloaded += this.OnUnloaded;

        this.updateChildSizesItemContainerGeneratorAction = new ItemContainerGeneratorAction(this.ItemContainerGenerator, this.UpdateChildSizes);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.SubscribeEvents();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.SetCurrentValue(IsDropDownOpenProperty, false);

        this.UnSubscribeEvents();
    }

    private void SubscribeEvents()
    {
        // Always unsubscribe events to ensure we don't subscribe twice
        this.UnSubscribeEvents();

        if (this.LauncherButton is not null)
        {
            this.LauncherButton.Click += this.OnDialogLauncherButtonClick;
        }

        if (this.DropDownPopup is not null)
        {
            this.DropDownPopup.Opened += this.OnPopupOpened;
            this.DropDownPopup.Closed += this.OnPopupClosed;
        }
    }

    private void UnSubscribeEvents()
    {
        if (this.LauncherButton is not null)
        {
            this.LauncherButton.Click -= this.OnDialogLauncherButtonClick;
        }

        if (this.DropDownPopup is not null)
        {
            this.DropDownPopup.Opened -= this.OnPopupOpened;
            this.DropDownPopup.Closed -= this.OnPopupClosed;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a panel with items
    /// </summary>
    /// <returns></returns>
    internal Panel? GetPanel()
    {
        return this.upPanel;
    }

    /// <summary>
    /// Gets cmmon layout root for popup and groupbox
    /// </summary>
    /// <returns></returns>
    internal Panel? GetLayoutRoot()
    {
        return this.parentPanel;
    }

    #endregion

    #region Snapping

    /// <summary>
    /// Snaps / Unsnaps the Visual
    /// (remove visuals and substitute with freezed image)
    /// </summary>
    public bool IsSnapped
    {
        get
        {
            return this.isSnapped;
        }

        set
        {
            if (value == this.isSnapped)
            {
                return;
            }

            if (value)
            {
                if (this.IsVisible)
                {
                    // Render the freezed image
                    var renderTargetBitmap = new RenderTargetBitmap((int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    renderTargetBitmap.Render((Visual)VisualTreeHelper.GetChild(this, 0));

                    if (this.snappedImage is not null)
                    {
                        this.snappedImage.FlowDirection = this.FlowDirection;
                        this.snappedImage.Source = renderTargetBitmap;
                        this.snappedImage.Width = this.ActualWidth;
                        this.snappedImage.Height = this.ActualHeight;
                        this.snappedImage.Visibility = Visibility.Visible;
                    }

                    this.isSnapped = true;
                }
            }
            else if (this.snappedImage is not null)
            {
                // Clean up
                this.snappedImage.Visibility = Visibility.Collapsed;
                this.isSnapped = false;
            }

            this.InvalidateVisual();
        }
    }

    #endregion

    #region Caching

    /// <summary>
    /// Gets or sets intermediate state of the group box
    /// </summary>
    internal RibbonGroupBoxState StateIntermediate { get; set; }

    /// <summary>
    /// Gets or sets intermediate scale of the group box
    /// </summary>
    internal int ScaleIntermediate { get; set; }

    /// <summary>
    /// Gets intermediate desired size
    /// </summary>
    internal Size GetDesiredSizeIntermediate()
    {
        var contentHeight = UIHelper.GetParent<RibbonTabControl>(this)?.ContentHeight ?? RibbonTabControl.DefaultContentHeight;

        using (this.CacheResetGuard.Start())
        {
            // Get desired size for these values
            this.State = this.StateIntermediate;
            this.Scale = this.ScaleIntermediate;
            this.InvalidateLayout();
            this.Measure(new Size(double.PositiveInfinity, contentHeight));
            return this.DesiredSize;
        }
    }

    internal bool TryClearCacheAndResetStateAndScale()
    {
        if (this.IsLoaded is false
            || this.CacheResetGuard.IsActive
            || this.State == RibbonGroupBoxState.QuickAccess)
        {
            return false;
        }

        this.State = RibbonGroupBoxState.Large;
        this.Scale = 0;
        this.StateIntermediate = RibbonGroupBoxState.Large;
        this.ScaleIntermediate = 0;

        this.ResetScaleableItems();

        return true;
    }

    /// <summary>
    /// Tries to clear the cache, reset the state and reset the scale.
    /// If that succeeds the parent <see cref="RibbonGroupsContainer"/> is notified about that.
    /// </summary>
    /// <returns><c>true</c> if the cache was reset. Otherwise <c>false</c>.</returns>
    public bool TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer()
    {
        // We should try to clear the entire cache.
        // The entire cache should only be cleared if we don't do regular measuring, but only if some event outside our own measuring code caused size changes (such as elements getting visible/invisible or being added/removed).
        // For reference https://github.com/fluentribbon/Fluent.Ribbon/issues/834
        if (this.TryClearCacheAndResetStateAndScale())
        {
            UIHelper.GetParent<RibbonGroupsContainer>(this)?.GroupBoxCacheClearedAndStateAndScaleResetted(this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears cache
    /// </summary>
    [Obsolete("This method does nothing anymore and will be removed in the next major version.")]
    public void ClearCache()
    {
    }

    /// <summary>
    /// Invalidates layout (with children)
    /// </summary>
    internal void InvalidateLayout()
    {
        InvalidateMeasureRecursive(this);
    }

    private static void InvalidateMeasureRecursive(UIElement element)
    {
        if (element.IsMeasureValid)
        {
            element.InvalidateMeasure();
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i) as UIElement;

            if (child is null)
            {
                continue;
            }

            InvalidateMeasureRecursive(child);
        }
    }

    #endregion

    #region Overrides

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        this.UnSubscribeEvents();

        // Clear cache
        this.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();

        this.HeaderContentControl = this.GetTemplateChild("PART_HeaderContentControl") as ContentControl;
        this.CollapsedHeaderContentControl = this.GetTemplateChild("PART_CollapsedHeaderContentControl") as ContentControl;

        this.LauncherButton = this.GetTemplateChild("PART_DialogLauncherButton") as Button;

        if (this.LauncherButton is not null)
        {
            if (this.LauncherKeys is not null)
            {
                this.LauncherButton.KeyTip = this.LauncherKeys;
            }
        }

        this.DropDownPopup = this.GetTemplateChild("PART_Popup") as Popup;

        this.upPanel = this.GetTemplateChild("PART_UpPanel") as Panel;
        this.parentPanel = this.GetTemplateChild("PART_ParentPanel") as Panel;

        this.snappedImage = this.GetTemplateChild("PART_SnappedImage") as Image;

        this.SubscribeEvents();
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        this.TryClearCacheAndResetStateAndScaleAndNotifyParentRibbonGroupsContainer();
        this.updateChildSizesItemContainerGeneratorAction.QueueAction();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.Source, this) == false
            || this.DropDownPopup is null)
        {
            return;
        }

        if (this.State == RibbonGroupBoxState.Collapsed
            || this.State == RibbonGroupBoxState.QuickAccess)
        {
            e.Handled = true;

            if (!this.IsDropDownOpen)
            {
                this.IsDropDownOpen = true;
            }
            else
            {
                PopupService.RaiseDismissPopupEventAsync(this, DismissPopupMode.MouseNotOver);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (this.IsInButtonState == false
            || e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                e.Handled = true;

                this.IsDropDownOpen = true;
                break;

            case Key.System:
                if (e.SystemKey == Key.Down
                    && e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    this.IsDropDownOpen = true;
                }

                break;

            case Key.Escape:
                e.Handled = true;
                this.IsDropDownOpen = false;
                break;
        }

        base.OnKeyDown(e);
    }

    #endregion

    #region Event Handling

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        this.DropDownOpened?.Invoke(this, e);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        this.DropDownClosed?.Invoke(this, e);
    }

    /// <summary>
    /// Dialog launcher button click handler
    /// </summary>
    /// <param name="sender">Sender</param>
    /// <param name="e">the event data</param>
    private void OnDialogLauncherButtonClick(object sender, RoutedEventArgs e)
    {
        this.LauncherClick?.Invoke(this, e);
    }

    /// <summary>
    /// Handles IsOpen propertyu changes
    /// </summary>
    /// <param name="d">Object</param>
    /// <param name="e">The event data</param>
    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var groupBox = (RibbonGroupBox)d;

        var oldValue = (bool)e.OldValue;
        var newValue = (bool)e.NewValue;

        groupBox.SetValue(System.Windows.Controls.ToolTipService.IsEnabledProperty, BooleanBoxes.Box(!newValue));

        groupBox.OnIsDropDownOpenChanged();

        (UIElementAutomationPeer.FromElement(groupBox) as Fluent.Automation.Peers.RibbonGroupBoxAutomationPeer)?.RaiseExpandCollapseAutomationEvent(oldValue, newValue);

        // todo: code in DropDownButton does nearly the same. Try to unify it.
        if (newValue)
        {
            if (groupBox.DropDownPopup is not null)
            {
                groupBox.RunInDispatcherAsync(
                    () =>
                    {
                        var container = groupBox.ItemContainerGenerator.ContainerFromIndex(0);
                        DropDownButton.NavigateToContainer(container);

                        // Edge case: Whole dropdown content is disabled
                        if (groupBox.IsKeyboardFocusWithin == false)
                        {
                            Keyboard.Focus(groupBox.DropDownPopup.Child);
                        }
                    });
            }
        }
        else
        {
            Keyboard.Focus(groupBox);
        }
    }

    private void OnIsDropDownOpenChanged()
    {
        if (this.IsDropDownOpen)
        {
            this.OnRibbonGroupBoxPopupOpening();
        }
        else
        {
            this.OnRibbonGroupBoxPopupClosing();
        }
    }

    // Handles popup closing
    private void OnRibbonGroupBoxPopupClosing()
    {
        //IsHitTestVisible = true;
        if (ReferenceEquals(Mouse.Captured, this))
        {
            Mouse.Capture(null);
        }
    }

    // handles popup opening
    private void OnRibbonGroupBoxPopupOpening()
    {
        //IsHitTestVisible = false;
        this.RunInDispatcherAsync(() => Mouse.Capture(this, CaptureMode.SubTree), DispatcherPriority.Loaded);
    }

    #endregion

    #region Quick Access Item Creating

    /// <inheritdoc />
    public virtual FrameworkElement CreateQuickAccessItem()
    {
        var groupBox = new RibbonGroupBox();

        RibbonControl.BindQuickAccessItem(this, groupBox);

        groupBox.DropDownOpened += this.OnQuickAccessOpened;
        groupBox.DropDownClosed += this.OnQuickAccessClosed;

        groupBox.State = RibbonGroupBoxState.QuickAccess;

        RibbonControl.Bind(this, groupBox, nameof(this.ItemTemplateSelector), ItemTemplateSelectorProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.ItemTemplate), ItemTemplateProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.ItemsSource), ItemsSourceProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherCommandParameter), LauncherCommandParameterProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherCommand), LauncherCommandProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherCommandTarget), LauncherCommandTargetProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherIcon), LauncherIconProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherText), LauncherTextProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherToolTip), LauncherToolTipProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.IsLauncherEnabled), IsLauncherEnabledProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.IsLauncherVisible), IsLauncherVisibleProperty, BindingMode.OneWay);
        RibbonControl.Bind(this, groupBox, nameof(this.LauncherKeys), LauncherKeysProperty, BindingMode.OneWay);
        groupBox.LauncherClick += this.LauncherClick;

        if (this.Icon is not null)
        {
            if (this.Icon is Visual iconVisual)
            {
                var rect = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = new VisualBrush(iconVisual)
                };
                groupBox.Icon = rect;
            }
            else
            {
                RibbonControl.Bind(this, groupBox, nameof(this.Icon), RibbonControl.IconProperty, BindingMode.OneWay);
            }
        }

        return groupBox;
    }

    private void OnQuickAccessOpened(object? sender, EventArgs e)
    {
        if (this.IsDropDownOpen == false
            && this.IsSnapped == false)
        {
            var groupBox = (RibbonGroupBox?)sender;
            // Save state
            this.IsSnapped = true;

            if (this.ItemsSource is null)
            {
                for (var i = 0; i < this.Items.Count; i++)
                {
                    var item = this.Items[0];
                    this.Items.Remove(item);
                    groupBox?.Items.Add(item);
                    i--;
                }
            }
        }
    }

    private void OnQuickAccessClosed(object? sender, EventArgs e)
    {
        var groupBox = (RibbonGroupBox?)sender;

        if (this.ItemsSource is null
            && groupBox is not null)
        {
            for (var i = 0; i < groupBox.Items.Count; i++)
            {
                var item = groupBox.Items[0];
                groupBox.Items.Remove(item);
                this.Items.Add(item);
                i--;
            }
        }

        this.IsSnapped = false;
    }

    /// <inheritdoc />
    public bool CanAddToQuickAccessToolBar
    {
        get { return (bool)this.GetValue(CanAddToQuickAccessToolBarProperty); }
        set { this.SetValue(CanAddToQuickAccessToolBarProperty, BooleanBoxes.Box(value)); }
    }

    /// <summary>Identifies the <see cref="CanAddToQuickAccessToolBar"/> dependency property.</summary>
    public static readonly DependencyProperty CanAddToQuickAccessToolBarProperty =
        DependencyProperty.Register(nameof(CanAddToQuickAccessToolBar), typeof(bool), typeof(RibbonGroupBox), new PropertyMetadata(BooleanBoxes.TrueBox, RibbonControl.OnCanAddToQuickAccessToolBarChanged));

    #endregion

    #region Implementation of IKeyTipedControl

    /// <inheritdoc />
    public KeyTipPressedResult OnKeyTipPressed()
    {
        if (this.State is RibbonGroupBoxState.Collapsed or RibbonGroupBoxState.QuickAccess)
        {
            this.IsDropDownOpen = true;

            return new KeyTipPressedResult(true, true);
        }

        return KeyTipPressedResult.Empty;
    }

    /// <inheritdoc />
    public void OnKeyTipBack()
    {
        this.IsDropDownOpen = false;
    }

    #endregion

    /// <inheritdoc />
    void ISimplifiedStateControl.UpdateSimplifiedState(bool isSimplified)
    {
        this.IsSimplified = isSimplified;
    }

    /// <inheritdoc />
    void ILogicalChildSupport.AddLogicalChild(object child)
    {
        this.AddLogicalChild(child);
    }

    /// <inheritdoc />
    void ILogicalChildSupport.RemoveLogicalChild(object child)
    {
        this.RemoveLogicalChild(child);
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren
    {
        get
        {
            var baseEnumerator = base.LogicalChildren;
            while (baseEnumerator?.MoveNext() == true)
            {
                yield return baseEnumerator.Current;
            }

            if (this.Icon is not null)
            {
                yield return this.Icon;
            }

            if (this.MediumIcon is not null)
            {
                yield return this.MediumIcon;
            }

            if (this.LargeIcon is not null)
            {
                yield return this.LargeIcon;
            }

            if (this.LauncherIcon is not null)
            {
                yield return this.LauncherIcon;
            }
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Fluent.Automation.Peers.RibbonGroupBoxAutomationPeer(this);
}