using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class JSONSpriteImporter : EditorWindow {

	private string json_location = "";
	private string prefix = "prefix";
	
	[MenuItem ("Window/JSON Sprite Import")]
	static void Init () 
	{
		// Get existing open window or if none, make a new one:
		JSONSpriteImporter window = (JSONSpriteImporter)EditorWindow.GetWindow (typeof (JSONSpriteImporter));
		window.Show();
	}
	
	void OnGUI () 
	{
		EditorGUILayout.BeginVertical ();

		EditorGUILayout.BeginHorizontal ();
		if (GUILayout.Button ("Load")) 
		{
			json_location = EditorUtility.OpenFilePanel(
				"Select a json file",
				"",
				"json");
		}
		GUILayout.Label ("File: ");
		GUILayout.Label (json_location ?? "no file loaded");
		EditorGUILayout.EndHorizontal ();

		EditorGUILayout.BeginHorizontal ();
		EditorGUILayout.PrefixLabel ("Sprite Prefix: ");
		prefix = EditorGUILayout.TextField (prefix);
		EditorGUILayout.EndHorizontal ();

		if(GUILayout.Button("Run"))
		{
			Run();
		}
		EditorGUILayout.EndVertical ();
	}

	private void Run()
	{
		string fileData = File.ReadAllText (json_location);
		JSONObject data = new JSONObject (fileData);

		Uri assetsPath = new Uri (Application.dataPath);
		Uri jsonPath = new Uri (System.IO.Path.GetDirectoryName (json_location));
		Uri diff = assetsPath.MakeRelativeUri (jsonPath);
		string wd = diff.OriginalString+System.IO.Path.DirectorySeparatorChar;


		//test data
		if(!data.HasFields(new string[]{"framerate","images","frames","animations"}))
		{
			Debug.LogError("Error: json file must contain framerate, images, frames, and animations.");
			return;
		}

		//generate sprite frames
		List<JSONObject> frames_data = data.GetField ("frames").list;
		List<JSONObject> images_data = data.GetField ("images").list;

		// load textures
		List<Texture2D> images = new List<Texture2D>();
		List<List<SpriteMetaData>> sprite_metadata = new List<List<SpriteMetaData>> ();
		for(int i=0; i<images_data.Count; i++)
		{
			string path = wd+images_data[i].str;
			Texture2D tex = AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
			images.Add(tex);
			sprite_metadata.Add (new List<SpriteMetaData> ());
		}

		//set meta data based on frames
		for(int i=0; i<frames_data.Count; i++)
		{
			List<JSONObject> frame = frames_data[i].list;
			float x = frame[0].f;
			float y = frame[1].f;
			float w = frame[2].f;
			float h = frame[3].f;
			int img = (int)frame[4].f;
			float rx = frame[5].f;
			float ry = frame[6].f;

			float imgHeight = (float)images[img].height;

			SpriteMetaData meta = new SpriteMetaData{
				alignment = (int)SpriteAlignment.Custom,
				border = new Vector4(),
				name = prefix+"_"+(i).ToString(),
				pivot = new Vector2(rx/w,1-(ry/h)),
				rect = new Rect(x, imgHeight - y - h, w, h)
			};
			sprite_metadata[img].Add(meta);
		}

		//save data back
		for(int i=0; i<images.Count; i++)
		{
			TextureImporter importer = TextureImporter.GetAtPath(wd+images_data[i].str) as TextureImporter;
            //importer.assetPath = images_data[i].str;
            importer.mipmapEnabled = false;
            importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Multiple;
			importer.spritesheet = sprite_metadata[i].ToArray();

			try
			{
				AssetDatabase.StartAssetEditing();
				AssetDatabase.ImportAsset(importer.assetPath);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
			}
		}

		//load sprite dictionary
		Dictionary<String,Sprite> sprites = new Dictionary<string, Sprite> ();
		for(int i=0; i<images_data.Count; i++)
		{
			Sprite[] sp = AssetDatabase.LoadAllAssetsAtPath( wd+images_data[i].str ).OfType<Sprite>().ToArray();
			for(int j=0; j<sp.Length; j++)
			{
				sprites[sp[j].name] = sp[j];
			}
		}

		//create animations
		int fps = (int)data.GetField ("framerate").f;
		List<string> animation_names = data.GetField ("animations").keys;

		foreach(string animationName in animation_names)
		{
			JSONObject animationJson = data.GetField("animations").GetField(animationName);
			List<JSONObject> frame_Data = animationJson.GetField("frames").list;
			float fpsinc = 1/(float)fps;

			EditorCurveBinding curveBinding = new EditorCurveBinding();
			curveBinding.type = typeof(SpriteRenderer);
			curveBinding.path = "";
			curveBinding.propertyName = "m_Sprite";

			List<ObjectReferenceKeyframe> keyframes = new List<ObjectReferenceKeyframe>();
			string lastFrame = "";
			for(int i=0; i<frame_Data.Count; i++)
			{
				string fname = frame_Data[i].f.ToString();
				if(i == frame_Data.Count-1 || fname!=lastFrame)
				{
					ObjectReferenceKeyframe k = new ObjectReferenceKeyframe();
					k.time = i*fpsinc;
					k.value = sprites[prefix+"_"+fname];
					keyframes.Add(k);
					lastFrame = fname;
				}
			}

			AnimationClip clip = new AnimationClip();
			clip.frameRate = (float)fps;
			clip.legacy = false;
			clip.EnsureQuaternionContinuity();

			AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyframes.ToArray());

            //load asset if it exists, else create new
            AnimationClip a = AssetDatabase.LoadAssetAtPath<AnimationClip>(wd + animationName + ".anim");
            if(a!= null)
            {
                Debug.Log("update clip");
                clip.wrapMode = a.wrapMode;
                a = clip;
            } else
            {
                AssetDatabase.CreateAsset(clip, wd + animationName + ".anim");
            }
		}
		AssetDatabase.SaveAssets();

	}
}
	
