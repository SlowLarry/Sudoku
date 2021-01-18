﻿using System;
using Microsoft.AspNetCore.Components;
using MudBlazor.Extensions;
using MudBlazor.Utilities;

namespace MudBlazor
{
    public partial class MudFabExt : MudBaseButton
    {
        protected string Classname =>
        new CssBuilder("mud-button-root mud-fab")
          .AddClass($"mud-fab-extended", !String.IsNullOrEmpty(Label))
          .AddClass($"mud-fab-{Color.ToDescriptionString()}")
          .AddClass($"mud-fab-size-{Size.ToDescriptionString()}")
          .AddClass($"mud-ripple", !DisableRipple && !Disabled)
          .AddClass($"mud-fab-disable-elevation", DisableElevation)
          .AddClass(Class)
        .Build();

        /// <summary>
        /// Child content of component.
        /// </summary>
        [Parameter] public RenderFragment ChildContent { get; set; }

        /// <summary>
        /// The color of the component. It supports the theme colors.
        /// </summary>
        [Parameter] public Color Color { get; set; } = Color.Default;

        /// <summary>
        /// The Size of the component.
        /// </summary>
        [Parameter] public Size Size { get; set; } = Size.Large;

        /// <summary>
        /// If applied Icon will be added to the component.
        /// </summary>
        [Parameter] public string Icon { get; set; }

        /// <summary>
        /// The color of the icon. It supports the theme colors.
        /// </summary>
        [Parameter] public Color IconColor { get; set; } = Color.Inherit;

        /// <summary>
        /// The size of the icon.
        /// </summary>
        [Parameter] public Size IconSize { get; set; } = Size.Medium;

        /// <summary>
        /// If applied the text will be added to the component.
        /// </summary>
        [Parameter] public string Label { get; set; }

        /// <summary>
        /// If true, the button will be disabled.
        /// </summary>
        [Parameter] public bool Disabled { get; set; }

        /// <summary>
        /// If true, no drop-shadow will be used.
        /// </summary>
        [Parameter] public bool DisableElevation { get; set; }

        /// <summary>
        /// If true, disables ripple effect.
        /// </summary>
        [Parameter] public bool DisableRipple { get; set; }

    }
}
