#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using GLFW;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	sealed class Sdl2PlatformWindow : ThreadAffine, IPlatformWindow
	{
		readonly IGraphicsContext context = null;
		readonly Sdl2Input input;

		public IGraphicsContext Context => context;

		readonly Window window;
		bool disposed;

		readonly object syncObject = new object();
		Size windowSize;
		Size surfaceSize;
		float windowScale = 1f;

		int2? lockedMousePosition;
		float scaleModifier;
		readonly GLProfile profile;
		readonly GLProfile[] supportedProfiles;

		internal Window Window
		{
			get
			{
				lock (syncObject)
					return window;
			}
		}

		public Size NativeWindowSize
		{
			get
			{
				lock (syncObject)
					return windowSize;
			}
		}

		public Size EffectiveWindowSize
		{
			get
			{
				lock (syncObject)
					return new Size((int)(windowSize.Width / scaleModifier), (int)(windowSize.Height / scaleModifier));
			}
		}

		public float NativeWindowScale
		{
			get
			{
				lock (syncObject)
					return windowScale;
			}
		}

		public float EffectiveWindowScale
		{
			get
			{
				lock (syncObject)
					return windowScale * scaleModifier;
			}
		}

		public Size SurfaceSize
		{
			get
			{
				lock (syncObject)
					return surfaceSize;
			}
		}

		public int CurrentDisplay => Glfw.Monitors.IndexOf(Glfw.GetWindowMonitor(window));

		public int DisplayCount => Glfw.Monitors.Length;

		public bool HasInputFocus { get; internal set; }

		public bool IsSuspended { get; internal set; }

		public GLProfile GLProfile
		{
			get
			{
				lock (syncObject)
					return profile;
			}
		}

		public GLProfile[] SupportedGLProfiles
		{
			get
			{
				lock (syncObject)
					return supportedProfiles;
			}
		}

		public event Action<float, float, float, float> OnWindowScaleChanged = (oldNative, oldEffective, newNative, newEffective) => { };

		[DllImport("user32.dll")]
		static extern bool SetProcessDPIAware();

		public Sdl2PlatformWindow(Size requestEffectiveWindowSize, WindowMode windowMode,
			float scaleModifier, int batchSize, int videoDisplay, GLProfile requestProfile, bool enableLegacyGL)
		{
			// Lock the Window/Surface properties until initialization is complete
			lock (syncObject)
			{
				this.scaleModifier = scaleModifier;

				// Disable legacy scaling on Windows
				if (Platform.CurrentPlatform == PlatformType.Windows)
					SetProcessDPIAware();

				/*
				// Decide which OpenGL profile to use.
				// Prefer standard GL over GLES provided by the native driver
				var testProfiles = new List<GLProfile> { GLProfile.ANGLE, GLProfile.Modern, GLProfile.Embedded };
				if (enableLegacyGL)
					testProfiles.Add(GLProfile.Legacy);

				supportedProfiles = testProfiles
					.Where(CanCreateGLWindow)
					.ToArray();

				if (!supportedProfiles.Any())
					throw new InvalidOperationException("No supported OpenGL profiles were found.");

				profile = supportedProfiles.Contains(requestProfile) ? requestProfile : supportedProfiles.First();
				*/

				supportedProfiles = new [] { GLProfile.Modern };
				profile = GLProfile.Modern;

				// Note: This must be called after the CanCreateGLWindow checks above,
				// which needs to create and destroy its own SDL contexts as a workaround for specific buggy drivers
				if (!Glfw.Init())
					throw new InvalidOperationException("Failed to initialized GLFW.");

				SetSDLAttributes(profile);

				Console.WriteLine("Using SDL 2 with OpenGL ({0}) renderer", profile);
				if (videoDisplay < 0 || videoDisplay >= DisplayCount)
					videoDisplay = 0;

				var display = Glfw.Monitors[videoDisplay];

				// Windows and Linux define window sizes in native pixel units.
				// Query the display/dpi scale so we can convert our requested effective size to pixels.
				// This is not necessary on macOS, which defines window sizes in effective units ("points").
				if (Platform.CurrentPlatform == PlatformType.Windows)
				{
					// Launch the game with OPENRA_DISPLAY_SCALE to force a specific scaling factor
					// Otherwise fall back to Windows's DPI configuration
					var scaleVariable = Environment.GetEnvironmentVariable("OPENRA_DISPLAY_SCALE");
					if (scaleVariable == null || !float.TryParse(scaleVariable, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out windowScale) || windowScale <= 0)
						windowScale = display.ContentScale.X;
				}
				else if (Platform.CurrentPlatform == PlatformType.Linux)
				{
					// Launch the game with OPENRA_DISPLAY_SCALE to force a specific scaling factor
					// Otherwise fall back to GDK_SCALE or parsing the x11 DPI configuration
					var scaleVariable = Environment.GetEnvironmentVariable("OPENRA_DISPLAY_SCALE") ?? Environment.GetEnvironmentVariable("GDK_SCALE");
					if (scaleVariable == null || !float.TryParse(scaleVariable, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out windowScale) || windowScale <= 0)
						windowScale = display.ContentScale.X;
				}

				Console.WriteLine("Desktop resolution: {0}x{1}", display.WorkArea.Width, display.WorkArea.Height);
				if (requestEffectiveWindowSize.Width == 0 && requestEffectiveWindowSize.Height == 0)
				{
					Console.WriteLine("No custom resolution provided, using desktop resolution");
					surfaceSize = windowSize = new Size(display.WorkArea.Width, display.WorkArea.Height);
				}
				else
					surfaceSize = windowSize = new Size((int)(requestEffectiveWindowSize.Width * windowScale), (int)(requestEffectiveWindowSize.Height * windowScale));

				Console.WriteLine("Using resolution: {0}x{1}", windowSize.Width, windowSize.Height);

				// HiDPI doesn't work properly on OSX with (legacy) fullscreen mode
				if (Platform.CurrentPlatform == PlatformType.OSX && windowMode == WindowMode.Fullscreen)
					Glfw.WindowHint(Hint.CocoaRetinaFrameBuffer, false);

				// TODO: Handle fullscreen monitors
				Glfw.WindowHint(Hint.Visible, true);
				window = Glfw.CreateWindow(windowSize.Width, windowSize.Height, "OpenRA", Monitor.None, Window.None);

				var screenSize = Glfw.PrimaryMonitor.WorkArea;
				var x = (screenSize.Width - windowSize.Width) / 2;
				var y = (screenSize.Height - windowSize.Height) / 2;
				Glfw.SetWindowPosition(window, x, y);

				// Work around an issue in macOS's GL backend where the window remains permanently black
				// (if dark mode is enabled) unless we drain the event queue before initializing GL
				if (Platform.CurrentPlatform == PlatformType.OSX)
				{
					Glfw.PollEvents();
					/*
					while (SDL.SDL_PollEvent(out var e) != 0)
					{
						// We can safely ignore all mouse/keyboard events and window size changes
						// (these will be caught in the window setup below), but do need to process focus
						if (e.type == SDL.SDL_EventType.SDL_WINDOWEVENT)
						{
							switch (e.window.windowEvent)
							{
								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
									HasInputFocus = false;
									break;

								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
									HasInputFocus = true;
									break;
							}
						}
					}
					*/
				}

				// Enable high resolution rendering for Retina displays
				/*
				if (false && Platform.CurrentPlatform == PlatformType.OSX)
				{
					// OSX defines the window size in "points", with a device-dependent number of pixels per point.
					// The window scale is simply the ratio of GL pixels / window points.
					SDL.SDL_GL_GetDrawableSize(Window, out var width, out var height);
					surfaceSize = new Size(width, height);
					windowScale = width * 1f / windowSize.Width;
				}
				else
				*/
				windowSize = new Size((int)(surfaceSize.Width / windowScale), (int)(surfaceSize.Height / windowScale));

				Console.WriteLine("Using window scale {0:F2}", windowScale);

				if (Game.Settings.Game.LockMouseWindow)
					GrabWindowMouseFocus();
				else
					ReleaseWindowMouseFocus();

				/*
				if (windowMode == WindowMode.Fullscreen)
				{
					SDL.SDL_SetWindowFullscreen(Window, (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN);

					// Fullscreen mode on OSX will ignore the configured display resolution
					// and instead always picks an arbitrary scaled resolution choice that may
					// not match the window size, leading to graphical and input issues.
					// We work around this by force disabling HiDPI and resetting the window and
					// surface sizes to match the size that is forced by SDL.
					// This is usually not what the player wants, but is the best we can consistently do.
					if (Platform.CurrentPlatform == PlatformType.OSX)
					{
						SDL.SDL_GetWindowSize(Window, out var width, out var height);
						windowSize = surfaceSize = new Size(width, height);
						windowScale = 1;
					}
				}
				else if (windowMode == WindowMode.PseudoFullscreen)
				{
					SDL.SDL_SetWindowFullscreen(Window, (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP);
					SDL.SDL_SetHint(SDL.SDL_HINT_VIDEO_MINIMIZE_ON_FOCUS_LOSS, "0");
				}
				*/
			}

			context = new ThreadedGraphicsContext(new Sdl2GraphicsContext(this), batchSize);

			context.SetVSyncEnabled(Game.Settings.Graphics.VSync);

			input = new Sdl2Input(this);
		}

		byte[] DoublePixelData(byte[] data, Size size)
		{
			var scaledData = new byte[4 * data.Length];
			for (var y = 0; y < size.Height; y++)
			{
				for (var x = 0; x < size.Width; x++)
				{
					var a = 4 * (y * size.Width + x);
					var b = 8 * (2 * y * size.Width + x);
					var c = b + 8 * size.Width;
					for (var i = 0; i < 4; i++)
						scaledData[b + i] = scaledData[b + 4 + i] = scaledData[c + i] = scaledData[c + 4 + i] = data[a + i];
				}
			}

			return scaledData;
		}

		public IHardwareCursor CreateHardwareCursor(string name, Size size, byte[] data, int2 hotspot, bool pixelDouble)
		{
			VerifyThreadAffinity();
			throw new Sdl2HardwareCursorException("not implemented");
			/*
			try
			{
				// Pixel double the cursor on non-OSX if the window scale is large enough
				// OSX does this for us automatically
				if (Platform.CurrentPlatform != PlatformType.OSX && NativeWindowScale > 1.5f)
				{
					data = DoublePixelData(data, size);
					size = new Size(2 * size.Width, 2 * size.Height);
					hotspot *= 2;
				}

				// Scale all but the "default" cursor if requested by the player
				if (pixelDouble)
				{
					data = DoublePixelData(data, size);
					size = new Size(2 * size.Width, 2 * size.Height);
					hotspot *= 2;
				}

				return new Sdl2HardwareCursor(size, data, hotspot);
			}
			catch (System.Exception ex)
			{
				throw new Sdl2HardwareCursorException("Failed to create hardware cursor `{0}` - {1}".F(name, ex.Message), ex);
			}
			*/
		}

		public void SetHardwareCursor(IHardwareCursor cursor)
		{
			/*
			VerifyThreadAffinity();
			if (cursor is Sdl2HardwareCursor c)
			{
				SDL.SDL_ShowCursor((int)SDL.SDL_bool.SDL_TRUE);
				SDL.SDL_SetCursor(c.Cursor);
			}
			else
				SDL.SDL_ShowCursor((int)SDL.SDL_bool.SDL_FALSE);
			*/
		}

		public void SetRelativeMouseMode(bool mode)
		{
			if (mode)
			{
				Glfw.GetCursorPosition(window, out var x, out var y);
				lockedMousePosition = new int2((int)x, (int)y);
			}
			else
			{
				if (lockedMousePosition.HasValue)
					Glfw.SetCursorPosition(window, lockedMousePosition.Value.X, lockedMousePosition.Value.Y);

				lockedMousePosition = null;
			}
		}

		internal void WindowSizeChanged()
		{
			/*
			// The ratio between pixels and points can change when moving between displays in OSX
			// We need to recalculate our scale to account for the potential change in the actual rendered area
			if (Platform.CurrentPlatform == PlatformType.OSX)
			{
				SDL.SDL_GL_GetDrawableSize(Window, out var width, out var height);

				if (width != SurfaceSize.Width || height != SurfaceSize.Height)
				{
					float oldScale;
					lock (syncObject)
					{
						oldScale = windowScale;
						surfaceSize = new Size(width, height);
						windowScale = width * 1f / windowSize.Width;
					}

					OnWindowScaleChanged(oldScale, oldScale * scaleModifier, windowScale, windowScale * scaleModifier);
				}
			}
			*/
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;

			context?.Dispose();

			// TODO: handle window destruction...
			// if (window != null)
			// 	Glfw.DestroyWindow(window);
			Glfw.Terminate();
		}

		public void GrabWindowMouseFocus()
		{
			/*
			VerifyThreadAffinity();
			SDL.SDL_SetWindowGrab(Window, SDL.SDL_bool.SDL_TRUE);
			*/
		}

		public void ReleaseWindowMouseFocus()
		{
			/*
			VerifyThreadAffinity();
			SDL.SDL_SetWindowGrab(Window, SDL.SDL_bool.SDL_FALSE);
			*/
		}

		public void PumpInput(IInputHandler inputHandler)
		{
			VerifyThreadAffinity();
			input.PumpInput(this, inputHandler, lockedMousePosition);

			/*
			if (lockedMousePosition.HasValue)
				SDL.SDL_WarpMouseInWindow(window, lockedMousePosition.Value.X, lockedMousePosition.Value.Y);
			*/
		}

		public string GetClipboardText()
		{
			VerifyThreadAffinity();
			return input.GetClipboardText();
		}

		public bool SetClipboardText(string text)
		{
			VerifyThreadAffinity();
			input.SetClipboardText(text);
			return true;
		}

		static void SetSDLAttributes(GLProfile profile)
		{
			Glfw.DefaultWindowHints();
			Glfw.WindowHint(Hint.Doublebuffer, true);
			Glfw.WindowHint(Hint.Decorated, true);
			Glfw.WindowHint(Hint.RedBits, 8);
			Glfw.WindowHint(Hint.GreenBits, 8);
			Glfw.WindowHint(Hint.BlueBits, 8);
			Glfw.WindowHint(Hint.AlphaBits, 8);
			Glfw.WindowHint(Hint.CocoaRetinaFrameBuffer, true);

			switch (profile)
			{
				case GLProfile.Modern:
					Glfw.WindowHint(Hint.ContextVersionMajor, 3);
					Glfw.WindowHint(Hint.ContextVersionMinor, 2);
					Glfw.WindowHint(Hint.OpenglProfile, Profile.Core);
					Glfw.WindowHint(Hint.OpenglForwardCompatible, true);
					Glfw.WindowHint(Hint.ClientApi, ClientApi.OpenGL);
					break;
				case GLProfile.ANGLE:
				case GLProfile.Embedded:
					Glfw.WindowHint(Hint.ContextVersionMajor, 3);
					Glfw.WindowHint(Hint.ContextVersionMinor, 0);
					Glfw.WindowHint(Hint.OpenglForwardCompatible, true);
					Glfw.WindowHint(Hint.ClientApi, ClientApi.OpenGLES);
					break;
				case GLProfile.Legacy:
					Glfw.WindowHint(Hint.ContextVersionMajor, 2);
					Glfw.WindowHint(Hint.ContextVersionMinor, 1);
					Glfw.WindowHint(Hint.OpenglProfile, Profile.Compatibility);
					Glfw.WindowHint(Hint.ClientApi, ClientApi.OpenGL);
					break;
			}
		}

		static bool CanCreateGLWindow(GLProfile profile)
		{
			if ((profile == GLProfile.Embedded || profile == GLProfile.ANGLE) && Platform.CurrentPlatform == PlatformType.OSX)
				return false;

			// Implementation inspired by TestIndividualGLVersion from Veldrid

			// Need to create and destroy its own SDL contexts as a workaround for specific buggy drivers
			if (!Glfw.Init())
				return false;

			try
			{
				SetSDLAttributes(profile);
				Glfw.WindowHint(Hint.Visible, false);
				var window = Glfw.CreateWindow(1, 1, "", Monitor.None, GLFW.Window.None);
				Glfw.MakeContextCurrent(window);

				// Distinguish between ANGLE and native GLES
				var success = true;
				if (profile == GLProfile.ANGLE || profile == GLProfile.Embedded)
					success = Glfw.GetExtensionSupported("GL_ANGLE_texture_usage") ^ (profile != GLProfile.ANGLE);

				// TODO: delete context?
				Glfw.DestroyWindow(window);
				Glfw.Terminate();

				return success;
			}
			catch (GLFW.Exception)
			{
				return false;
			}
			catch (System.Exception)
			{
				return false;
			}
		}

		public void SetScaleModifier(float scale)
		{
			var oldScaleModifier = scaleModifier;
			scaleModifier = scale;
			OnWindowScaleChanged(windowScale, windowScale * oldScaleModifier, windowScale, windowScale * scaleModifier);
		}
	}
}
