using UnityEngine;
using TMPro;

public class NameText : MonoBehaviour
{
    private TMP_Text nameText;
    private PlayerController player;

    void Awake()
    {
        nameText = GetComponent<TMP_Text>();
        player = GetComponentInParent<PlayerController>();
    }

    void Update()
    {
        if(player != null)
        {
            nameText.text = player.playerName;
        }
    }
}