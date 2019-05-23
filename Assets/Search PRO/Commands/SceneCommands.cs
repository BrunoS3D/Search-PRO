﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityObject = UnityEngine.Object;


namespace SearchPRO {
	public static class SceneCommands {

		[Command("Save Scene", "Opens a dialog window to save the current scene.")]
		public static void CreateCube() {
			EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
		}
	}
}

#endif
