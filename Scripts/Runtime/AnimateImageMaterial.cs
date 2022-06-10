using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class AnimateImageMaterial : MonoBehaviour
{
    public bool executeAlways = true;
    public bool createMaterialInstance = true;
    // Values in arrays and lists cannot be animated
    public string property1;
    public float value1;
    private Image image;
    [SerializeField]
    private Material material;

    private void Awake()
    {
        material = null;
    }

    private void OnEnable()
    {
        image = GetComponent<Image>();
        if (Application.isPlaying && material == null)
        {
            material = Instantiate(image.material);
            image.material = material;
        }
        else material = image.material;
    }

    // Undocumented monobehaviour method
    private void OnDidApplyAnimationProperties()
    {
        if (!Application.isPlaying && !executeAlways) return;
        if (material) material.SetFloat(property1, value1);
    }
}
