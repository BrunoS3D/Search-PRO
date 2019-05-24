﻿#if UNITY_EDITOR
using UnityEngine;
using UnityObject = UnityEngine.Object;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SearchPRO {
	public class SearchEditorWindow : EditorWindow {

		private class Styles {

			public readonly Texture search_icon;

			public readonly GUIStyle window_background = (GUIStyle)"grey_border";

			public readonly GUIStyle label = EditorStyles.label;

			public readonly GUIStyle scroll_shadow;

			public readonly GUIStyle tag_button;

			public readonly GUIStyle search_bar;

			public readonly GUIStyle search_label;

			public readonly GUIStyle search_icon_item;

			public readonly GUIStyle search_title_item;

			public readonly GUIStyle search_description_item;

			public readonly GUIStyle on_search_title_item;

			public readonly GUIStyle on_search_description_item;

			public Styles() {
				search_icon = EditorGUIUtility.FindTexture("Search Icon");

				scroll_shadow = GlobalSkin.scrollShadow;

				tag_button = new GUIStyle(EditorStyles.miniButton);
				tag_button.richText = true;

				search_bar = GlobalSkin.searchBar;
				search_label = GlobalSkin.searchLabel;
				search_icon_item = GlobalSkin.searchIconItem;
				search_title_item = GlobalSkin.searchTitleItem;
				search_description_item = GlobalSkin.searchDescriptionItem;

				on_search_title_item = new GUIStyle(GlobalSkin.searchTitleItem);
				on_search_description_item = new GUIStyle(GlobalSkin.searchDescriptionItem);

				on_search_title_item.normal = GlobalSkin.searchTitleItem.onNormal;
				on_search_description_item.normal = GlobalSkin.searchDescriptionItem.onNormal;

				on_search_title_item.hover = GlobalSkin.searchTitleItem.onHover;
				on_search_description_item.hover = GlobalSkin.searchDescriptionItem.onHover;
			}
		}


		private static Styles styles;


		private const float WINDOW_HEAD_HEIGHT = 80.0f;

		private const float WINDOW_FOOT_OFFSET = 10.0f;

		private const string PREFS_ENABLE_TAGS = "SearchPRO: SEW EnableTags Toggle";

		private const string PREFS_ELEMENT_SIZE_SLIDER = "SearchPRO: SEW ElementSize Slider";

		private readonly Color FOCUS_COLOR = new Color(62.0f / 255.0f, 125.0f / 255.0f, 231.0f / 255.0f);

		private readonly Color STRIP_COLOR_DARK = new Color(0.205f, 0.205f, 0.205f);

		private readonly Color STRIP_COLOR_LIGHT = new Color(0.7f, 0.7f, 0.7f);

		private readonly Color WINDOW_HEAD_COLOR = new Color(0.7f, 0.7f, 0.7f);



		private string search;

		private string new_search;

		private bool need_refocus;

		private bool drag_scroll;

		private bool enable_already;

		private bool enable_layout;

		private bool enable_scroll;

		private bool register_undo;

		private float scroll_pos;

		private float element_list_height = 35;

		public int selected_index;

		private int view_element_capacity;

		public TreeNode<SearchItem> root_tree;

		public TreeNode<SearchItem> current_tree;

		public TreeNode<SearchItem> last_tree;

		public TreeNode<SearchItem> selected_node;

		public readonly List<TreeNode<SearchItem>> parents = new List<TreeNode<SearchItem>>();

		public bool enableTags {
			get {
				return EditorPrefs.GetBool(PREFS_ENABLE_TAGS, true);
			}
			set {
				EditorPrefs.SetBool(PREFS_ENABLE_TAGS, value);
			}
		}

		public float sliderValue {
			get {
				return EditorPrefs.GetFloat(PREFS_ELEMENT_SIZE_SLIDER, 35);
			}
			set {
				EditorPrefs.SetFloat(PREFS_ELEMENT_SIZE_SLIDER, value);
			}
		}

		public bool hasSearch {
			get {
				return !search.IsNullOrEmpty();
			}
		}

		[MenuItem("Window/Search PRO %SPACE")]
		public static SearchEditorWindow Init() {
			SearchEditorWindow editor = CreateInstance<SearchEditorWindow>();
			editor.wantsMouseMove = true;
			editor.ShowPopup();
			editor.RecalculateSize();
			FocusWindowIfItsOpen<SearchEditorWindow>();
			return editor;
		}

		void OnEnable() {
			if (!enable_already) {
				//Unity bug fix
				Undo.undoRedoPerformed += Close;
				if (styles == null) {
					styles = new Styles();
				}

				need_refocus = true;
				element_list_height = sliderValue;

				root_tree = new TreeNode<SearchItem>(new GUIContent("Home"), null);

				// Pega todos os types na assembly atual
				foreach (Type type in ReflectionUtils.GetTypesFrom(GetType().Assembly)) {
					// Pega todos os methods do type atual
					foreach (MethodInfo method in type.GetMethodsFrom()) {
						// Pega e verifica a existencia do attributo
						CommandAttribute a_command = null;
						CategoryAttribute a_category = null;
						TitleAttribute a_title = null;
						DescriptionAttribute a_description = null;
						TagsAttribute a_tags = null;

						string category = string.Empty;
						string title = string.Empty;
						string description = string.Empty;
						string[] tags = new string[] { };

						foreach (Attribute attribute in method.GetCustomAttributes()) {
							if (a_command == null) {
								if (attribute is CommandAttribute) {
									a_command = (CommandAttribute)attribute;
									continue;
								}
							}
							else {
								if (a_category == null && attribute is CategoryAttribute) {
									a_category = (CategoryAttribute)attribute;
									a_command.category = a_category;
									category = a_category.category;
									continue;
								}
								if (a_title == null && attribute is TitleAttribute) {
									a_title = (TitleAttribute)attribute;
									a_command.title = a_title;
									title = a_title.title;
									continue;
								}
								if (a_description == null && attribute is DescriptionAttribute) {
									a_description = (DescriptionAttribute)attribute;
									a_command.description = a_description;
									description = a_description.description;
									continue;
								}
								if (a_tags == null && attribute is TagsAttribute) {
									a_tags = (TagsAttribute)attribute;
									a_command.tags = a_tags;
									tags = a_tags.tags;
									continue;
								}
							}
						}

						if (a_command != null) {
							Validation validation = Validation.None;

							// Realiza a verificacao dos parametros e se o methodo pode ser chamado
							foreach (ParameterInfo param in method.GetParameters()) {
								Type param_type = param.ParameterType;

								// Verifica se o tipo do parametro
								if (typeof(string).IsAssignableFrom(param_type)) {
									validation = Validation.searchInput;
								}
								else if (typeof(GameObject).IsAssignableFrom(param_type)) {
									validation = Validation.activeGameObject;
								}
								else if (typeof(GameObject[]).IsAssignableFrom(param_type)) {
									validation = Validation.gameObjects;
								}
								else if (typeof(Transform).IsAssignableFrom(param_type)) {
									validation = Validation.activeTransform;
								}
								else if (typeof(Transform[]).IsAssignableFrom(param_type)) {
									validation = Validation.transforms;
								}
								else if (typeof(UnityObject).IsAssignableFrom(param_type)) {
									validation = Validation.activeObject;
								}
								else if (typeof(UnityObject[]).IsAssignableFrom(param_type)) {
									validation = Validation.objects;
								}
							}

							if (a_category == null) {
								root_tree.AddChildByPath(new GUIContent(title, description), new CommandItem(a_command, method, validation), tags);
							}
							else {
								root_tree.AddChildByPath(new GUIContent(string.Format("{0}/{1}", category, title), description), new CommandItem(a_command, method, validation), tags);
							}
						}
					}
				}

				GoToHome();
				enable_already = true;
			}
		}

		void OnDisable() {
			//Unity bug fix
			Undo.undoRedoPerformed -= Close;
		}

		void OnGUI() {
			if (focusedWindow != this || EditorApplication.isCompiling) {
				Close();
			}
			if (!enable_already) {
				OnEnable();
			}

			GUI.Box(new Rect(0.0f, 0.0f, base.position.width, base.position.height), GUIContent.none, styles.window_background);

			EditorGUI.DrawRect(new Rect(1.0f, 1.0f, base.position.width - 2.0f, WINDOW_HEAD_HEIGHT - 1.0f), WINDOW_HEAD_COLOR);

			view_element_capacity = (int)((position.height - (WINDOW_HEAD_HEIGHT + (WINDOW_FOOT_OFFSET * 2))) / element_list_height);

			KeyboardInputGUI();
			RefreshSearchControl();

			Rect search_rect = new Rect(15.0f, 10.0f, position.width - 30.0f, 30.0f);
			Rect search_icon_rect = new Rect(20.0f, 13.0f, 23.0f, 23.0f);

			// Search Bar 
			GUI.SetNextControlName("GUIControlSearchBoxTextField");
			new_search = GUI.TextField(search_rect, new_search, styles.search_bar);

			if (need_refocus) {
				GUI.FocusControl("GUIControlSearchBoxTextField");
				TextEditor txt = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
				if (txt != null) {
					txt.MoveLineEnd();
					txt.SelectNone();
				}
				need_refocus = false;
			}

			GUI.DrawTexture(search_icon_rect, styles.search_icon);

			if (enable_layout) {
				GUILayout.Space(50.0f);

				GUILayout.BeginHorizontal();
				{
					if (hasSearch && current_tree.content.text == "#Search") {
						if (GUILayout.Button(new GUIContent("Home"), GUILayout.ExpandWidth(false), GUILayout.Height(20.0f))) {
							GoToHome();
						}
					}
					for (int id = parents.Count - 1; id >= 0; id--) {
						TreeNode<SearchItem> parent = parents[id];
						if (GUILayout.Button(parent.content.text, GUILayout.ExpandWidth(false), GUILayout.Height(20.0f))) {
							GoToNode(parent, true);
							break;
						}
					}
					GUILayout.Button(current_tree.content.text, GUILayout.ExpandWidth(false), GUILayout.Height(20.0f));
				}
				GUILayout.EndHorizontal();
			}

			int current_tree_count = current_tree == null ? 0 : current_tree.Count;

			enable_scroll = view_element_capacity < current_tree_count;

			Rect list_area = new Rect(1.0f, WINDOW_HEAD_HEIGHT, position.width - (enable_scroll ? 19.0f : 2.0f), position.height - (WINDOW_HEAD_HEIGHT + WINDOW_FOOT_OFFSET));

			if (enable_scroll) {
				scroll_pos = GUI.VerticalScrollbar(new Rect(position.width - 17.0f, WINDOW_HEAD_HEIGHT, 20.0f, list_area.height), scroll_pos, 1.0f, 0.0f, current_tree_count - view_element_capacity + 1);
			}
			else {
				scroll_pos = 0.0f;
			}
			scroll_pos = Mathf.Clamp(scroll_pos, 0.0f, current_tree_count);

			if (GUI.Toggle(new Rect(position.width - 60.0f, 55.0f, 50.0f, 20.0f), enableTags, "Tags") != enableTags) {
				enableTags = !enableTags;
			}

			sliderValue = Mathf.Round(GUI.HorizontalSlider(new Rect(position.width - 60.0f, 40.0f, 50.0f, 20.0f), sliderValue, 25, 50) / 5) * 5;

			GUI.BeginClip(list_area);

			PreInputGUI();

			int first_scroll_index = (int)Mathf.Clamp(scroll_pos, 0, current_tree_count);
			int last_scroll_index = (int)Mathf.Clamp(scroll_pos + view_element_capacity + 2, 0, current_tree_count);

			int draw_index = 0;
			for (int id = first_scroll_index; id < last_scroll_index; id++) {
				bool selected = false;

				TreeNode<SearchItem> node = current_tree[id];
				float smooth_offset = (scroll_pos - id) * element_list_height;

				Rect layout_rect = new Rect(0.0f, draw_index - smooth_offset, list_area.width, element_list_height);
				if (id % 2 == 1) {
					if (EditorGUIUtility.isProSkin) {
						EditorGUI.DrawRect(layout_rect, STRIP_COLOR_DARK);
					}
					else {
						EditorGUI.DrawRect(layout_rect, STRIP_COLOR_LIGHT);
					}
				}

				//Draw Selection Box
				if (selected_index == draw_index + first_scroll_index || (Event.current.type == EventType.MouseMove && layout_rect.Contains(Event.current.mousePosition))) {
					selected = true;
					selected_node = node;
					selected_index = draw_index + first_scroll_index;
					EditorGUI.DrawRect(layout_rect, FOCUS_COLOR);
				}

				if (enable_layout) {
					if (enableTags) {
						//Draw Tag Buttons
						GUILayout.BeginArea(new Rect(layout_rect.x, layout_rect.y + 5.0f, layout_rect.width, layout_rect.height));
						GUILayout.BeginHorizontal();
						GUILayout.FlexibleSpace();
						foreach (string tag in node.tags) {
							if (GUILayout.Button(HighlightText(tag, search), styles.tag_button, GUILayout.ExpandWidth(false))) {
								new_search = tag;
								need_refocus = true;
							}
						}
						GUILayout.EndHorizontal();
						GUILayout.EndArea();
					}
				}

				//Draw Element Button
				if (DrawElementList(layout_rect, node.content, selected)) {
					GoToNode(node, true);
					break;
				}
				draw_index++;
			}
			PostInputGUI();

			GUI.EndClip();

			if (enable_scroll && scroll_pos != 0.0f) {
				Color gui_color = GUI.color;
				if (scroll_pos < 1.0f) {
					GUI.color = new Color(gui_color.r, gui_color.g, gui_color.b, gui_color.a * scroll_pos);
				}
				GUI.Box(new Rect(0.0f, WINDOW_HEAD_HEIGHT, position.width - 15.0f, 10.0f), GUIContent.none, styles.scroll_shadow);
				GUI.color = gui_color;
			}

			if (Event.current.type == EventType.Repaint) {
				enable_layout = true;
			}
			Repaint();
		}

		void GoToHome() {
			GoToNode(root_tree, false);
			new_search = string.Empty;
			GUI.FocusControl("GUIControlSearchBoxTextField");
			need_refocus = true;
		}

		void GoToParent() {
			if (!current_tree.isRoot) {
				GoToNode(current_tree.parent, false);
			}
		}

		void GoToNode(TreeNode<SearchItem> node, bool call_if_is_leaf) {
			if (node == null) return;
			enable_layout = false;

			if (node.isLeaf) {
				if (call_if_is_leaf) {
					ExecuteItem(node.data);
					this.Close();
				}
				else {
					GUI.FocusControl("GUIControlSearchBoxTextField");
					need_refocus = true;
				}
			}
			else {
				if (current_tree != null && !current_tree.isSearch) {
					last_tree = current_tree;
				}
				current_tree = node;
				if (last_tree == null) {
					last_tree = current_tree;
				}

				parents.Clear();
				TreeNode<SearchItem> parent = current_tree.parent;
				//while parent isNotRoot
				while (parent != null) {
					parents.Add(parent);
					parent = parent.parent;
				}

				selected_index = 0;
				scroll_pos = 0.0f;
				RecalculateSize();
			}
		}

		void ExecuteItem(SearchItem item) {
			if (item is CommandItem) {
				CommandItem command = (CommandItem)item;
				switch (command.validation) {
					default:
					case Validation.None:
					command.method.Invoke(null, null);
					break;

					case Validation.searchInput:
					command.method.Invoke(null, new object[] { search });
					break;

					case Validation.activeGameObject:
					if (Selection.activeGameObject) {
						command.method.Invoke(null, new object[] { Selection.activeGameObject });
					}
					break;

					case Validation.gameObjects:
					if (Selection.gameObjects.Length > 0) {
						command.method.Invoke(null, new object[] { Selection.gameObjects });
					}
					break;

					case Validation.activeTransform:
					if (Selection.activeTransform) {
						command.method.Invoke(null, new object[] { Selection.activeTransform });
					}
					break;

					case Validation.transforms:
					if (Selection.transforms.Length > 0) {
						command.method.Invoke(null, new object[] { Selection.transforms });
					}
					break;

					case Validation.activeObject:
					if (Selection.activeObject) {
						command.method.Invoke(null, new object[] { Selection.activeGameObject });
					}
					break;

					case Validation.objects:
					if (Selection.objects.Length > 0) {
						command.method.Invoke(null, new object[] { Selection.objects });
					}
					break;
				}
			}
			else {
				Selection.activeObject = EditorUtility.InstanceIDToObject((int)item.data);
			}
			Close();
		}

		void RefreshSearchControl() {
			if (search != new_search) {
				if (new_search.IsNullOrEmpty()) {
					GoToNode(last_tree, false);
				}
				else {
					TreeNode<SearchItem> search_result = last_tree;
					search_result = search_result.GetTreeNodeInAllChildren(tn =>
					ValidateItem(tn.data)
					&& Regex.IsMatch(tn.content.text, Regex.Escape(new_search), RegexOptions.IgnoreCase)
							|| Regex.IsMatch(tn.content.tooltip, Regex.Escape(new_search), RegexOptions.IgnoreCase)
							|| (enableTags && tn.tags.Any(tag => Regex.IsMatch(new_search, Regex.Escape(tag), RegexOptions.IgnoreCase))));

					GoToNode(search_result, false);
				}
				search = new_search;
				RecalculateSize();
			}
		}

		bool ValidateItem(SearchItem item) {
			if (item is CommandItem) {
				CommandItem command = (CommandItem)item;
				switch (command.validation) {
					default:
					case Validation.None:
					return true;

					case Validation.activeGameObject:
					if (Selection.activeGameObject) {
						return true;
					}
					return false;

					case Validation.gameObjects:
					if (Selection.gameObjects.Length > 0) {
						return true;
					}
					return false;

					case Validation.activeTransform:
					if (Selection.activeTransform) {
						return true;
					}
					return false;

					case Validation.transforms:
					if (Selection.transforms.Length > 0) {
						return true;
					}
					return false;

					case Validation.activeObject:
					if (Selection.activeObject) {
						return true;
					}
					return false;

					case Validation.objects:
					if (Selection.objects.Length > 0) {
						return true;
					}
					return false;
				}
			}
			return true;
		}

		public string HighlightText(string text, string format) {
			if (text.IsNullOrEmpty() || format.IsNullOrEmpty()) return text;
			return Regex.Replace(text, format, (match) => string.Format("<color=#FFFF00><b>{0}</b></color>", match), RegexOptions.IgnoreCase);
		}

		public bool DrawElementList(Rect rect, GUIContent content, bool selected) {
			Rect layout_rect = new Rect(rect);
			bool trigger = GUI.Button(layout_rect, string.Empty, styles.label);

			Rect icon_rect = new Rect(layout_rect.x + 10.0f, layout_rect.y, element_list_height, element_list_height);
			Rect title_rect = new Rect(element_list_height + 5.0f, layout_rect.y, layout_rect.width - element_list_height - 10.0f, layout_rect.height);
			Rect subtitle_rect = new Rect(title_rect);

			GUI.Label(icon_rect, content.image, styles.search_icon_item);
			if (!search.IsNullOrEmpty()) {
				string title = HighlightText(content.text, search);
				EditorGUI.LabelField(title_rect, title, selected ? styles.on_search_title_item : styles.search_title_item);

				if (sliderValue > 30) {
					string subtitle = HighlightText(content.tooltip, search);
					//string subtitle = content.tooltip.Replace(search, string.Format("<color=#ffff00ff><b>{0}</b></color>", search));
					EditorGUI.LabelField(subtitle_rect, subtitle, selected ? styles.on_search_description_item : styles.search_description_item);
				}
			}
			else {
				EditorGUI.LabelField(title_rect, content.text, selected ? styles.on_search_title_item : styles.search_title_item);
				if (sliderValue > 30) {
					EditorGUI.LabelField(subtitle_rect, content.tooltip, selected ? styles.on_search_description_item : styles.search_description_item);
				}
			}

			return !drag_scroll && trigger;
		}

		void PreInputGUI() {
			Event current = Event.current;

			switch (current.type) {
				case EventType.MouseDown:
				drag_scroll = false;
				break;
				case EventType.ScrollWheel:
				drag_scroll = true;
				scroll_pos += current.delta.y;
				current.Use();
				break;
				case EventType.MouseDrag:
				drag_scroll = true;
				scroll_pos -= current.delta.y / element_list_height;
				current.Use();
				break;
			}
		}

		void PostInputGUI() {
			Event current = Event.current;

			switch (current.type) {
				case EventType.KeyDown:
				break;
				case EventType.MouseUp:
				drag_scroll = false;
				break;
			}
		}

		void KeyboardInputGUI() {
			Event current = Event.current;

			switch (current.type) {
				case EventType.MouseUp:
				if (element_list_height != sliderValue) {
					element_list_height = sliderValue;
					RecalculateSize();
				}
				break;
				case EventType.KeyDown:
				if (current.keyCode == KeyCode.Escape) {
					this.Close();
				}
				if (!current.control) {
					//char current_char = Event.current.character;
					//if (char.IsNumber(current_char)) {
					//	selected_index = (int)(scroll_pos + (char.GetNumericValue(current_char))) - 1;
					//	if (selected_index < 0) {
					//		selected_index = 0;
					//	}
					//	else if (selected_index >= search_items.Count) {
					//		selected_index = search_items.Count - 1;
					//		scroll_pos = search_items.Count;
					//	}
					//	else if (selected_index >= scroll_pos + view_element_capacity) {
					//		scroll_pos += Mathf.Abs(selected_index - view_element_capacity);
					//	}
					//	current.Use();
					//}
					//else {
					if (current.keyCode == KeyCode.Home) {
						selected_index = 0;
						scroll_pos = 0.0f;
						current.Use();
					}
					else if (current.keyCode == KeyCode.End) {
						selected_index = current_tree.Count - 1;
						scroll_pos = current_tree.Count;
						current.Use();
					}
					else if (current.keyCode == KeyCode.PageDown) {
						selected_index += view_element_capacity;
						scroll_pos += view_element_capacity;
						if (selected_index >= current_tree.Count) {
							selected_index = 0;
							scroll_pos = 0.0f;
						}
						current.Use();
					}
					else if (current.keyCode == KeyCode.PageUp) {
						selected_index -= view_element_capacity;
						scroll_pos -= view_element_capacity;
						if (selected_index < 0) {
							selected_index = current_tree.Count - 1;
							scroll_pos = current_tree.Count;
						}
						current.Use();
					}
					else if (current.keyCode == KeyCode.DownArrow) {
						selected_index++;
						if (selected_index >= scroll_pos + view_element_capacity) {
							scroll_pos++;
						}
						if (selected_index >= current_tree.Count) {
							selected_index = 0;
							scroll_pos = 0.0f;
						}
						current.Use();
					}
					else if (current.keyCode == KeyCode.UpArrow) {
						selected_index--;
						if (selected_index < scroll_pos) {
							scroll_pos--;
						}
						if (selected_index < 0) {
							selected_index = current_tree.Count - 1;
							scroll_pos = current_tree.Count;
						}
						current.Use();
					}
					else if ((current.keyCode == KeyCode.LeftArrow) || (current.keyCode == KeyCode.Backspace)) {
						if (!hasSearch) {
							this.GoToParent();
							current.Use();
						}
					}
					else if (current.keyCode == KeyCode.RightArrow) {
						if (!hasSearch) {
							this.GoToNode(selected_node, false);
							current.Use();
						}
					}
					else if ((current.keyCode == KeyCode.Return) || (current.keyCode == KeyCode.KeypadEnter)) {
						this.GoToNode(selected_node, true);
					}
					//}
				}
				break;
			}
		}

		void RecalculateSize() {
			enable_layout = false;
			float width = 0.0f;
			foreach (TreeNode<SearchItem> node in current_tree) {
				float tags_width = 0.0f;
				if (enableTags) {
					foreach (string tag in node.tags) {
						tags_width += GUIUtils.GetTextWidth(tag, styles.tag_button) + 5.0f;
					}
				}
				width = Mathf.Max(width, GUIUtils.GetTextWidth(node.content.text, styles.search_title_item) + 85.0f + tags_width);
			}
			width = Mathf.Max(Screen.currentResolution.width / 2.0f, width);
			Vector2 pos = new Vector2(Screen.currentResolution.width / 2.0f - width / 2.0f, 100.0f);
			Vector2 size = new Vector2(width, Mathf.Min(WINDOW_HEAD_HEIGHT + (current_tree.Count * element_list_height) + (WINDOW_FOOT_OFFSET * 2.0f), Screen.currentResolution.height - pos.y - 150.0f));

			position = new Rect(pos, size);
		}
	}
}
#endif

