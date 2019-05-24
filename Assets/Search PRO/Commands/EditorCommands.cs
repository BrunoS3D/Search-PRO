﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityObject = UnityEngine.Object;
using System;
using System.Text.RegularExpressions;

namespace SearchPRO {
	public static class EditorCommands {

		[Command]
		[Category("Editor")]
		[Title("Play")]
		[Description("Enter in playmode.")]
		[Tags("EAP")]
		public static void Play() {
			EditorApplication.ExecuteMenuItem("Edit/Play");
		}

		[Command]
		[Category("Editor")]
		[Title("Stop")]
		[Description("Stops playmode.")]
		[Tags("EAS")]
		public static void Stop() {
			if (EditorApplication.isPlaying) {
				EditorApplication.ExecuteMenuItem("Edit/Play");
			}
		}

		[Command]
		[Category("Editor")]
		[Title("Pause")]
		[Description("Pause current game.")]
		[Tags("EAU")]
		public static void Pause() {
			EditorApplication.ExecuteMenuItem("Edit/Pause");
		}

		[Command]
		[Category("Editor")]
		[Title("Step")]
		[Description("Step to the next frame.")]
		[Tags("EAT")]
		public static void Step() {
			EditorApplication.ExecuteMenuItem("Edit/Step");
		}
	}
}

#endif
