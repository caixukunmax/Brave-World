namespace ClinetCSharp
{
    public static class DebugPanelTransientFocusPolicy
    {
        public static bool ShouldReleaseOnLeftMousePress(string? focusOwnerClassName, bool pointerStillOnFocusOwner)
        {
            return IsTransientDragControl(focusOwnerClassName) && !pointerStillOnFocusOwner;
        }

        public static bool ShouldReleaseOnLeftMouseRelease(string? focusOwnerClassName)
        {
            return IsTransientDragControl(focusOwnerClassName);
        }

        public static bool ShouldDisableFocusMode(string? className)
        {
            return IsTransientDragControl(className);
        }

        public static bool ShouldStartPanelResize(string? hoveredClassName)
        {
            return !IsTransientDragControl(hoveredClassName);
        }

        private static bool IsTransientDragControl(string? className)
        {
            return className is "Slider"
                or "HSlider"
                or "VSlider"
                or "ScrollBar"
                or "HScrollBar"
                or "VScrollBar";
        }
    }
}