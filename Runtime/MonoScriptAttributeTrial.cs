using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class MonoScriptAttributeTrial : MonoBehaviour
{
    [MonoScript(typeof(ExamplePrivateClass))] [SerializeField]
    private string _classType;

    [ValueDropdown("GetListOfMonoBehaviours", AppendNextDrawer = true)] [SerializeField]
    private MonoBehaviour _someBehaviour;

    private IEnumerable<MonoBehaviour> GetListOfMonoBehaviours()
    {
        return GameObject.FindObjectsOfType<MonoBehaviour>();
    }

    [Button]
    public void PrintType()
    {
        Debug.Log($"{_classType}");
    }

    private class ExamplePrivateClass
    {

    }

    private class ExamplePrivateClassImpl1 : ExamplePrivateClass
    {
    }

    private class ExamplePrivateClassImpl2 : ExamplePrivateClass
    {
    }
}