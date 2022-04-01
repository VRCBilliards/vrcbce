/*
This file is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to 
https://www.gamedevblog.com/2012/12/unity-editor-script-for-replacing-shaders.html
*/
using UnityEngine;

using UnityEditor;

using System.Collections;

// ReplaceShader 
// Gathers all the materials used in the scene, 
// and then replaces all shaders of a specified type with another shader. 

namespace SilentTools
{
public class ReplaceShader : ScriptableWizard
{
	public Shader oldShader;
	public Shader newShader;
	
	[MenuItem("Tools/Replace Shader")]

	static void CreateWizard ()
	{
		ScriptableWizard .DisplayWizard<ReplaceShader>  ("Replace Shader", "ReplaceShader");
	}

	void OnWizardCreate ()
	{
		//Transform[] Replaces;
		//Replaces = Replace.GetComponentsInChildren();
		Renderer[] renderers = GameObject.FindSceneObjectsOfType (typeof(Renderer)) as Renderer[];
		foreach (var renderer in renderers) {
			foreach (Material m in renderer.sharedMaterials) {
				if (m != null && m.shader == oldShader) {
					m.shader = newShader;
					Debug.Log (m.name);
				}
			}

		}
	}
}
}