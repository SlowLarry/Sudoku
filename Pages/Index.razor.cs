﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SudokuBlazor.Models;
using SudokuBlazor.Shared;

namespace SudokuBlazor.Pages
{
    partial class Index
    {
        // Element References
        private ElementReference sudokusvg;

        // Constants
        private const double cellRectWidth = SudokuConstants.cellRectWidth;

        // State
        private bool hasRendered = false;

        // Input
        private bool mouseDown = false;
        private long touchIdDown = -1;
        private double inputLastX = 0.0;
        private double inputLastY = 0.0;

        // Components
        private SudokuSelection selection;
        private SudokuValues values;
        private SudokuKeypad keypad;

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                keypad.NumpadPressedAction = NumpadKeyPressed;
            }
        }

        protected override bool ShouldRender()
        {
            if (!hasRendered)
            {
                hasRendered = true;
                return true;
            }
            return false;
        }

        protected async Task SelectCellAtLocation(double clientX, double clientY, bool controlDown, bool shiftDown, bool altDown)
        {
            var infoFromJs = await JS.InvokeAsync<string>("getSVG_XY", sudokusvg, clientX, clientY);
            var values = infoFromJs.Split(" ");
            double x = double.Parse(values[0]);
            double y = double.Parse(values[1]);

            int i = (int)Math.Floor(y / cellRectWidth);
            int j = (int)Math.Floor(x / cellRectWidth);
            selection.SelectCell(i, j, controlDown, shiftDown, altDown);
        }

        protected async Task InputStart(double clientX, double clientY, bool ctrlKey, bool shiftKey, bool altKey)
        {
            if (!ctrlKey && !shiftKey && !altKey)
            {
                selection.SelectNone();
            }
            selection.ResetLastSelectedCell();
            await SelectCellAtLocation(clientX, clientY, ctrlKey, shiftKey, altKey);
            inputLastX = clientX;
            inputLastY = clientY;

            if (selection.HasSelectedCells())
            {
                await SetFocus(sudokusvg);
            }
        }

        protected async Task MouseDown(MouseEventArgs e)
        {
            if (touchIdDown != -1 || mouseDown)
            {
                return;
            }

            mouseDown = true;
            await InputStart(e.ClientX, e.ClientY, e.CtrlKey, e.ShiftKey, e.AltKey);
        }

        protected async Task TouchStart(TouchEventArgs e)
        {
            if (e.ChangedTouches.Length == 0 || mouseDown)
            {
                return;
            }

            TouchPoint touch = null;
            if (touchIdDown != -1)
            {
                foreach (var curTouch in e.ChangedTouches)
                {
                    if (curTouch.Identifier == touchIdDown)
                    {
                        touch = curTouch;
                        break;
                    }
                }
                if (touch == null)
                {
                    return;
                }
            }
            else
            {
                touch = e.ChangedTouches[0];
                touchIdDown = touch.Identifier;
            }

            await InputStart(touch.ClientX, touch.ClientY, e.CtrlKey, e.ShiftKey, e.AltKey);
        }

        protected async Task InputMove(double clientX, double clientY, bool ctrlKey, bool shiftKey, bool altKey)
        {
            var boundingRect = await GetBoundingClientRect(sudokusvg);

            double mouseDiffX = clientX - inputLastX;
            double mouseDiffY = clientY - inputLastY;
            double mouseDiffLen = Math.Sqrt(mouseDiffX * mouseDiffX + mouseDiffY * mouseDiffY);
            double stepSizeX = boundingRect.Width / 18.0;
            double stepSizeY = boundingRect.Height / 18.0;
            if (mouseDiffLen > stepSizeX && mouseDiffLen > stepSizeY)
            {
                double mouseDiffInvLen = 1.0 / Math.Sqrt(mouseDiffX * mouseDiffX + mouseDiffY * mouseDiffY);
                mouseDiffX *= stepSizeX * mouseDiffInvLen;
                mouseDiffY *= stepSizeY * mouseDiffInvLen;
                inputLastX += mouseDiffX;
                inputLastY += mouseDiffY;
                while ((mouseDiffX > 0.1 && inputLastX < clientX || mouseDiffX < 0.1 && inputLastX > clientX)
                    || (mouseDiffY > 0.1 && inputLastY < clientY || mouseDiffY < 0.1 && inputLastY > clientY))
                {
                    await SelectCellAtLocation(inputLastX, inputLastY, ctrlKey, shiftKey, altKey);
                    inputLastX += mouseDiffX;
                    inputLastY += mouseDiffY;
                }
            }

            await SelectCellAtLocation(clientX, clientY, ctrlKey, shiftKey, altKey);
            inputLastX = clientX;
            inputLastY = clientY;
        }

        protected async Task MouseMove(MouseEventArgs e)
        {
            if (mouseDown)
            {
                await InputMove(e.ClientX, e.ClientY, e.CtrlKey, e.ShiftKey, e.AltKey);
            }
        }

        protected async Task TouchMove(TouchEventArgs e)
        {
            if (touchIdDown != -1)
            {
                foreach (var touch in e.ChangedTouches)
                {
                    if (touch.Identifier == touchIdDown)
                    {
                        await InputMove(touch.ClientX, touch.ClientY, e.CtrlKey, e.ShiftKey, e.AltKey);
                    }
                }
            }
        }

        protected async Task InputEnd(double clientX, double clientY, bool ctrlKey, bool shiftKey, bool altKey)
        {
            await SelectCellAtLocation(clientX, clientY, ctrlKey, shiftKey, altKey);
        }

        protected async Task MouseUp(MouseEventArgs e)
        {
            if (mouseDown)
            {
                await InputEnd(e.ClientX, e.ClientY, e.CtrlKey, e.ShiftKey, e.AltKey);
                mouseDown = false;
            }
        }

        protected async Task TouchEnd(TouchEventArgs e)
        {
            if (touchIdDown != -1)
            {
                foreach (var touch in e.ChangedTouches)
                {
                    if (touch.Identifier == touchIdDown)
                    {
                        await InputEnd(touch.ClientX, touch.ClientY, e.CtrlKey, e.ShiftKey, e.AltKey);
                        touchIdDown = -1;
                        return;
                    }
                }
            }
        }

        private enum KeyCodeType
        {
            DeleteCell,
            Value,
            A,
            MoveUp,
            MoveDown,
            MoveLeft,
            MoveRight,
            Ignore
        }
        private static (KeyCodeType, int) GetKeyCodeType(string keyCode)
        {
            switch (keyCode)
            {
                case "Delete":
                case "Backspace":
                case "Digit0":
                case "Numpad0":
                    return (KeyCodeType.DeleteCell, 0);
                case "KeyA": // a
                    return (KeyCodeType.A, -1);
                case "ArrowUp":
                    return (KeyCodeType.MoveUp, -1);
                case "ArrowDown":
                    return (KeyCodeType.MoveDown, -1);
                case "ArrowLeft":
                    return (KeyCodeType.MoveLeft, -1);
                case "ArrowRight":
                    return (KeyCodeType.MoveRight, -1);
            }
            if (keyCode.StartsWith("Digit"))
            {
                return (KeyCodeType.Value, keyCode[5] - '0');
            }
            if (keyCode.StartsWith("Numpad"))
            {
                return (KeyCodeType.Value, keyCode[6] - '0');
            }
            return (KeyCodeType.Ignore, -1);
        }

        protected void KeyDown(KeyboardEventArgs e)
        {
            var (keyCodeType, value) = GetKeyCodeType(e.Code);
            switch (keyCodeType)
            {
                case KeyCodeType.DeleteCell:
                    foreach (int cellIndex in selection.SelectedCellIndices())
                    {
                        values.ClearCell(cellIndex);
                    }
                    return;
                case KeyCodeType.A:
                    if (e.CtrlKey)
                    {
                        selection.SelectAll();
                    }
                    return;
                case KeyCodeType.Value:
                    foreach (int cellIndex in selection.SelectedCellIndices())
                    {
                        values.SetCellValue(cellIndex, value);
                    }
                    return;
                case KeyCodeType.MoveUp:
                    selection.Move(SudokuSelection.MoveDir.Up, e.CtrlKey, e.ShiftKey, e.AltKey);
                    return;
                case KeyCodeType.MoveDown:
                    selection.Move(SudokuSelection.MoveDir.Down, e.CtrlKey, e.ShiftKey, e.AltKey);
                    return;
                case KeyCodeType.MoveLeft:
                    selection.Move(SudokuSelection.MoveDir.Left, e.CtrlKey, e.ShiftKey, e.AltKey);
                    return;
                case KeyCodeType.MoveRight:
                    selection.Move(SudokuSelection.MoveDir.Right, e.CtrlKey, e.ShiftKey, e.AltKey);
                    return;

            }
        }

        protected void FocusLost()
        {
            selection.SelectNone();
        }

        protected void NumpadKeyPressed(int value)
        {
            foreach (int cellIndex in selection.SelectedCellIndices())
            {
                values.SetCellValue(cellIndex, value);
            }
        }

        private async Task<BoundingClientRect> GetBoundingClientRect(ElementReference element)
        {
            return await JS.InvokeAsync<BoundingClientRect>("getBoundingClientRect", element);
        }

        private async Task SetFocus(ElementReference element)
        {
            await JS.InvokeVoidAsync("setFocusToElement", element);
        }
    }
}
