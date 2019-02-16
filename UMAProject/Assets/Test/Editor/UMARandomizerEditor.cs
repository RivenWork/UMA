﻿using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UMA.CharacterSystem;


namespace UMA.Editors
{
	[CustomEditor(typeof(UMARandomizer))]
	public class UMARandomizerEditor : Editor
	{
		private int currentRace = 0;
		private string[] races;
		private List<RaceData> raceDatas;
		UMARandomizer currentTarget = null;
		private bool changed = false;

		List<UMAWardrobeRecipe> droppedItems = new List<UMAWardrobeRecipe>();

		public void OnEnable()
		{
			currentTarget = target as UMARandomizer;
			currentRace = 0;
			List<string> Races = new List<string>();
			raceDatas = new List<RaceData>();

			string[] guids = AssetDatabase.FindAssets("t:racedata");

			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string name = Path.GetFileNameWithoutExtension(path);
				RaceData rc = AssetDatabase.LoadAssetAtPath<RaceData>(path);
				raceDatas.Add(rc);
				Races.Add(rc.raceName);
			}
			races = Races.ToArray();
		}

		protected bool DropAreaGUI(Rect dropArea)
		{

			var evt = Event.current;

			if (evt.type == EventType.DragUpdated)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				}
			}

			if (evt.type == EventType.DragPerform)
			{
				droppedItems.Clear();
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.AcceptDrag();

					UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences as UnityEngine.Object[];
					for (int i = 0; i < draggedObjects.Length; i++)
					{
						if (draggedObjects[i])
						{
							if (draggedObjects[i] is UMAWardrobeRecipe)
							{
								UMAWardrobeRecipe utr = draggedObjects[i] as UMAWardrobeRecipe;
								droppedItems.Add(utr);
								continue;
							}

							var path = AssetDatabase.GetAssetPath(draggedObjects[i]);
							if (System.IO.Directory.Exists(path))
							{
								RecursiveScanFoldersForAssets(path);
							}
						}
					}
				}
			}
			return droppedItems.Count > 0;
		}

		protected void RecursiveScanFoldersForAssets(string path)
		{
			var assetFiles = System.IO.Directory.GetFiles(path, "*.asset");
			foreach (var assetFile in assetFiles)
			{
				var tempRecipe = AssetDatabase.LoadAssetAtPath(assetFile, typeof(UMAWardrobeRecipe)) as UMAWardrobeRecipe;
				if (tempRecipe)
				{
					droppedItems.Add(tempRecipe);
				}
			}
			foreach (var subFolder in System.IO.Directory.GetDirectories(path))
			{
				RecursiveScanFoldersForAssets(subFolder.Replace('\\', '/'));
			}
		}

		public void RandomColorsGUI(RandomAvatar ra, RandomWardrobeSlot rws, RandomColors rc)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Shared Color", GUILayout.Width(80));
			rc.CurrentColor = EditorGUILayout.Popup(rc.CurrentColor, rws.PossibleColors,GUILayout.Width(80));
			rc.ColorName = rws.PossibleColors[rc.CurrentColor];
			EditorGUILayout.LabelField("Color Table", GUILayout.Width(80));
			rc.ColorTable = (SharedColorTable)EditorGUILayout.ObjectField(rc.ColorTable, typeof(SharedColorTable),false,GUILayout.ExpandWidth(true));
			EditorGUILayout.EndHorizontal();
		}

		public void RandomWardrobeSlotGUI(RandomAvatar ra, RandomWardrobeSlot rws)
		{
			// do random colors
			// show each possible item.
			GUIHelper.FoldoutBar(ref rws.GuiFoldout, rws.WardrobeSlot.name + " ("+rws.WardrobeSlot.wardrobeSlot+")", out rws.Delete);
			if (rws.GuiFoldout)
			{
				GUIHelper.BeginVerticalPadded(10, new Color(0.75f, 0.75f, 0.75f));
				rws.Chance = EditorGUILayout.IntSlider("Weighted Chance",rws.Chance, 1, 100);
				if (rws.PossibleColors.Length > 0)
				{
					if (GUILayout.Button("Add Shared Color"))
					{
						rws.AddColorTable = true;
					}
					foreach (RandomColors rc in rws.Colors)
					{
						RandomColorsGUI(ra, rws, rc);
					}
				}
				else
				{
					GUILayout.Label("Wardrobe Recipe has no Shared Colors");
				}
				GUIHelper.EndVerticalPadded(10);
			}
		}

		public void RandomAvatarGUI(RandomAvatar ra)
		{
			bool del = false;
			GUIHelper.FoldoutBar(ref ra.GuiFoldout, ra.RaceName, out del);
			if (ra.GuiFoldout)
			{
				GUIHelper.BeginVerticalPadded(10, new Color(0.75f, 0.875f, 1f));
				if (del)
				{
					ra.Delete = true;
				}

				ra.Chance = EditorGUILayout.IntSlider("Weighted Chance", ra.Chance, 1, 100);
				foreach (RandomWardrobeSlot rws in ra.RandomWardrobeSlots)
				{
					RandomWardrobeSlotGUI(ra,rws);
				}
				GUIHelper.EndVerticalPadded(10);
			}
		}


		public override void OnInspectorGUI()
		{
			if (Event.current.type == EventType.Layout)
			{
				UpdateObject();
			}
			currentRace = EditorGUILayout.Popup("Race", currentRace, races);

			GUILayout.Space(20);
			Rect updateDropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
			GUI.Box(updateDropArea, "Drag Wardrobe Recipe(s) for "+ races[currentRace] + " here");
			GUILayout.Space(10);
			DropAreaGUI(updateDropArea);
			GUILayout.Space(10);
			foreach(RandomAvatar ra in currentTarget.RandomAvatars)
			{
				RandomAvatarGUI(ra);
			}
		}

	
		private void UpdateObject()
		{
			// Add any dropped items.
			int ChangeCount = droppedItems.Count;

			if (droppedItems.Count > 0)
			{
				foreach(RandomAvatar rv in currentTarget.RandomAvatars)
				{
					rv.GuiFoldout = false;
					foreach(RandomWardrobeSlot rws in rv.RandomWardrobeSlots)
					{
						rws.GuiFoldout = false;
					}
				}

				RandomAvatar ra = FindAvatar(raceDatas[currentRace]);

				// Add all the wardrobe items.
				foreach (UMAWardrobeRecipe uwr in droppedItems)
				{
					if (RecipeCompatible(uwr, raceDatas[currentRace]))
					{
						RandomWardrobeSlot rws = new RandomWardrobeSlot(uwr);
						ra.GuiFoldout = true;
						ra.RandomWardrobeSlots.Add(rws);
					}
				}
				// sort the wardrobe slots
				ra.RandomWardrobeSlots.Sort((x, y) => x.WardrobeSlot.wardrobeSlot.CompareTo(y.WardrobeSlot.wardrobeSlot));
				droppedItems.Clear();
			}

			ChangeCount += currentTarget.RandomAvatars.RemoveAll(x => x.Delete);
			foreach(RandomAvatar ra in currentTarget.RandomAvatars)
			{
				ChangeCount += ra.SharedColors.RemoveAll(x => x.Delete);
				ChangeCount += ra.RandomWardrobeSlots.RemoveAll(x => x.Delete);
				foreach(RandomWardrobeSlot rws in ra.RandomWardrobeSlots)
				{
					ChangeCount += rws.Colors.RemoveAll(x => x.Delete);
					if (rws.AddColorTable)
					{
						rws.Colors.Add(new RandomColors(rws));
						rws.AddColorTable = false;
					}
				}
			}

			if (ChangeCount > 0)
			{
				EditorUtility.SetDirty(currentTarget);
				AssetDatabase.SaveAssets();
			}
		}

		private bool RecipeCompatible(UMAWardrobeRecipe uwr, RaceData raceData)
		{
			// first, see if the recipe is directly compatible with the race.
			foreach (string s in uwr.compatibleRaces)
			{
				if (s == raceData.raceName)
				{
					return true;
				}
				if (raceData.IsCrossCompatibleWith(s))
				{
					return true;
				}
			}
			return false;
		}

		private RandomAvatar FindAvatar(RaceData raceData)
		{
			// Is the current race defined?
			foreach (RandomAvatar ra in currentTarget.RandomAvatars)
			{
				if (raceData.raceName == ra.RaceName)
				{
					return ra;
				}
			}
			RandomAvatar rav = new RandomAvatar(raceData.raceName);

			currentTarget.RandomAvatars.Add(rav);
			return rav;
		}
	}
}
