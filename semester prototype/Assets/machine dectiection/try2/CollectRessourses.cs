using TMPro;
using UnityEngine;

public class CollectRessourses : MonoBehaviour
{
    public int iron = 0;
    public int plast = 0;
    public int electronic = 0;
    
    [SerializeField] private TMP_Text ironText;
    [SerializeField] private TMP_Text plastText;
    [SerializeField] private TMP_Text electronicText;

    public void ironRes()
    {
        
        iron++;
        Debug.Log(iron);
        ironText.text = iron.ToString();
    }


    public void plastRes()
    {
     
        plast++;
        Debug.Log(plast);
        plastText.text = plast.ToString();

    }

    public void electronicRes()
    {
        
        electronic++;
        Debug.Log(electronic);
        electronicText.text = electronic.ToString();

    }



    public void minusironRes()
    {

        iron--;
        Debug.Log(iron);
        ironText.text = iron.ToString();
    }


    public void minusplastRes()
    {

        plast--;
        Debug.Log(plast);
        plastText.text = plast.ToString();

    }

    public void minuselectronicRes()
    {

        electronic--;
        Debug.Log(electronic);
        electronicText.text = electronic.ToString();

    }

}
