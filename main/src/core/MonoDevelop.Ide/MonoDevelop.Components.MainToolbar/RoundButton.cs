// 
// RoundButton.cs
//  
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using Gtk;
using Gdk;
using MonoDevelop.Components;
using Cairo;
using MonoDevelop.Ide;
using System.Reflection;


namespace MonoDevelop.Components.MainToolbar
{
	class LazyImage
	{
		string resourceName;

		ImageSurface img;
		public ImageSurface Img {
			get {
				if (img == null)
					img = CairoExtensions.LoadImage (Assembly.GetCallingAssembly (), resourceName);
				return img;
			}
		}

		public LazyImage (string resourceName)
		{
			this.resourceName = resourceName;
		}

		public static implicit operator ImageSurface(LazyImage lazy)
		{
			return lazy.Img;
		}

	}

	class RoundButton : Gtk.EventBox
	{
		const int height = 32;
/*		Cairo.Color borderColor;

		Cairo.Color fill0Color;
		Cairo.Color fill1Color;
		Cairo.Color fill2Color;
		Cairo.Color fill3Color;*/

		LazyImage btnNormal, btnPressed, btnInactive, btnHover;

		LazyImage iconRunNormal, iconRunDisabled;
		LazyImage iconStopNormal, iconStopDisabled;

		public RoundButton ()
		{
			WidgetFlags |= Gtk.WidgetFlags.AppPaintable;
			Events |= EventMask.ButtonPressMask | EventMask.ButtonReleaseMask | EventMask.LeaveNotifyMask | EventMask.PointerMotionMask;
			VisibleWindow = false;
			SetSizeRequest (height, height);

			btnNormal = new LazyImage ("btExecuteBase-Normal.png");
			btnInactive = new LazyImage ("btExecuteBase-Disabled.png");
			btnPressed = new LazyImage ("btExecuteBase-Pressed.png");
			btnHover = new LazyImage ("btExecuteBase-Hover.png");

			iconRunNormal = new LazyImage ("icoExecute-Normal.png");
			iconRunDisabled = new LazyImage ("icoExecute-Disabled.png");

			iconStopNormal = new LazyImage ("icoStop-Normal.png");
			iconStopDisabled = new LazyImage ("icoStop-Disabled.png");
		}

		StateType hoverState = StateType.Normal;

		protected override bool OnMotionNotifyEvent (EventMotion evnt)
		{

			State = IsInside (evnt.X, evnt.Y) ? hoverState : StateType.Normal;;
			return base.OnMotionNotifyEvent (evnt);
		}


		protected override bool OnLeaveNotifyEvent (EventCrossing evnt)
		{
			State = StateType.Normal;
			return base.OnLeaveNotifyEvent (evnt);
		}

		protected override bool OnButtonPressEvent (EventButton evnt)
		{
			if (evnt.Button == 1 && IsInside (evnt.X, evnt.Y)) {
				hoverState = State = StateType.Selected;
			}
			return base.OnButtonPressEvent (evnt);
		}

		protected override bool OnButtonReleaseEvent (EventButton evnt)
		{
			if (State == StateType.Selected)
				OnClicked (EventArgs.Empty);
			State = IsInside (evnt.X, evnt.Y) ? StateType.Prelight : StateType.Normal;;
			hoverState = StateType.Prelight; 
			return base.OnButtonReleaseEvent (evnt);
		}

		bool IsInside (double x, double y)
		{
			var xr = x - Allocation.Width / 2;
			var yr = y - Allocation.Height / 2;
			return Math.Sqrt (xr * xr + yr * yr) <= height / 2;
		}

		protected override void OnSizeRequested (ref Requisition requisition)
		{
			requisition.Width = btnNormal.Img.Width;
			requisition.Height = btnNormal.Img.Height + 2;
			base.OnSizeRequested (ref requisition);
		}


		ImageSurface GetBackground()
		{
			switch (State) {
				case StateType.Selected:
					return btnPressed;
				case StateType.Prelight:
					return btnHover;
				case StateType.Insensitive:
					return btnInactive;
				default:
					return btnNormal;
				}
		}

		ImageSurface GetIcon()
		{
			if (!ShowStart) {
				return State ==  StateType.Insensitive ? iconStopDisabled : iconStopNormal;
			}
			return State ==  StateType.Insensitive ? iconRunDisabled : iconRunNormal;
		}

		bool showStart;
		public bool ShowStart {
			get { return showStart; }
			set {
				if (value != showStart) {
					showStart = value;
					QueueDraw ();
				}
			}
		}

		protected override bool OnExposeEvent (EventExpose evnt)
		{
			using (var context = Gdk.CairoHelper.Create (evnt.Window)) {
				GetBackground ().Show (context, Allocation.X, Allocation.Y);
				var icon = GetIcon();
				icon.Show (
					context,
					Allocation.X + (icon.Width - Allocation.Width) / 2,
					Allocation.Y + (icon.Height - Allocation.Height) / 2
				);
			}
			return base.OnExposeEvent (evnt);
		}

		public event EventHandler Clicked;

		protected virtual void OnClicked (EventArgs e)
		{
			EventHandler handler = this.Clicked;
			if (handler != null)
				handler (this, e);
		}
	}
}

