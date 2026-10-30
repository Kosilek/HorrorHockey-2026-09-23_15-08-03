using UnityEngine;

public class GetPosition : MonoBehaviour
{
    [SerializeField] private string _name;

    private void Start()
    {
        Debug.Log($"Name: {_name}. Position: {transform.position}");
    }
}
