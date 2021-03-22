using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class mat_replacement
{
	[SerializeField] public string name;
	[SerializeField] public bool shown;

	[SerializeField] public Shader shader_default;
	[SerializeField] public Shader shader_quest;

	[SerializeField] public List< Material > materials;
}

[System.Serializable]
public class obj_toggly
{
	[SerializeField] public string name;
	[SerializeField] public bool shown;

	[SerializeField] public bool pc;
	[SerializeField] public bool quest;

	[SerializeField] public List< GameObject > objs;
}

public class quest_stuff : MonoBehaviour
{
	[SerializeField] public List< mat_replacement > replacements;
	[SerializeField] public List< obj_toggly > objs;
}
