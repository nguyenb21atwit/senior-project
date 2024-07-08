using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;
using Scene = UnityEngine.SceneManagement.Scene;

public class Card : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    //Class Stuff
    public string cardName;
    public int cardID;
    public int apCost;
    public string effectText;
    public bool playerCard;
    private bool upgradable;

    //Tooltip Stuff
    public TextMeshProUGUI tooltipText;
    public GameObject tooltipPanel;
    private bool isTooltipActive;
    Vector3 offScreenPosition = new Vector3(-10000, -10000, 0);
    //Adjust these numbers if you need to change where the tooltip is
    private const float tooltipOffsetX = 0f; // Tooltip Offset in the X axis
    private const float tooltipOffsetY = 9.5f; // Tooltip Offset in the Y axis

    public bool hasBeenPlayed;
    public int handIndex;
    private FightManager fm;
    private Player player;
    private Enemy enemy;
    private DiceRoller diceRoller;
    private Dictionary<int, Func<bool>> cardDictionary;

    //Particle FX
    public ParticleSystem playEfx;
    public ParticleSystem burnEfx;

    public int shopCost;


    void Awake()
    {
        IntializeCardDictionary();
        fm = FindObjectOfType<FightManager>();
        player = FindObjectOfType<Player>();
        enemy = FindObjectOfType<Enemy>();
        diceRoller = FindObjectOfType<DiceRoller>();
        tooltipPanel = GameObject.Find("TooltipPanel");
        tooltipText = tooltipPanel.GetComponentInChildren<TextMeshProUGUI>();
    }
    // Start is called before the first frame update
    void Start()
    {
        HideTooltip();
    }
    void Update()
    {
        if (isTooltipActive)
        {
            UpdateTooltipPosition();
        }
    }

    //ALL PLAYING/BURNING/CARD CONTROLLERS//
    void IntializeCardDictionary()
    {
        cardDictionary = new Dictionary<int, Func<bool>>()
        {
            { 0, () => NoEffect()},
            { 1, () => Heal(10)}, //Recover
            { 2, () => Reroll(5)}, //Loaded Dice
            { 3, () => GrantFullHouse()}, //Steady Aim
            { 4, () => ActivateShield(20, 1)}, //Fortify
            { 5, () => ChangeMaxAP(1) }, //Booster Energy
            { 6, () => DoubleDamage() },  //Deadeye
            { 7, () => ChooseReroll(3,false) },  //Weighted Dice
            { 8, () => ChooseReroll(3,true) },  // 3 of diamonds
            { 9, () => EnergyDrain(1) }
        };
    }
    public bool ApplyEffect()
    {
        if (cardDictionary.TryGetValue(cardID, out Func<bool> effect))
        {
            return effect.Invoke();
        }
        else
        {
            Debug.LogWarning($"No effect found for cardID: {cardID}");
            return false;
        }
    }
    void PlayCard(int actionPointCost)
    {
        if (actionPointCost <= player.actionPoints)
        {
            fm.playPlayCardSound();
            if (ApplyEffect() == true)
            {
                Vector3 cardPosition = transform.position;
                player.discardPile.Add(this);
                gameObject.SetActive(false);
                Instantiate(playEfx, cardPosition, Quaternion.identity);

                HideTooltip();
                for (int i = 0; i < actionPointCost; i++)
                {
                    player.actionPoints--;
                    fm.UpdateActionPoints();
                }
                hasBeenPlayed = true;
                fm.availableCardSlots[handIndex] = true;
                Debug.Log("Card played");
            }
            else
            {
                Debug.Log("Card conditions not met and cannot be played!");
                ShowTooltip(effectText);
            }
        }
        else
        {
            Debug.Log("Card cannot be played due to insufficient AP!");
            ShowTooltip(effectText);
        }
    }
    void BurnCard()
    {
        if (player.actionPoints < player.maxActionPoints)
        {
            fm.playBurnCardSound();
            player.actionPoints = player.actionPoints + 1;
            fm.UpdateActionPoints();
            Debug.Log("Card burned");
        }
        else
        {
            Debug.Log("AP already at maximum value. Discarding Card.");
        }
        Vector3 cardPosition = transform.position;
        player.discardPile.Add(this);
        gameObject.SetActive(false);
        Instantiate(burnEfx, cardPosition, Quaternion.identity);
        HideTooltip();
        hasBeenPlayed = true;
        fm.availableCardSlots[handIndex] = true;
    }

    void UpgradeCard(bool canUpgrade, int cardID)
    {
        if (canUpgrade)
        {
            //do stuff
            player.deck.Remove(this);
            gameObject.SetActive(false);
        }
    }

    //ALL CARD EFFECTS FUNCTIONS//
    bool Heal(int amount) //needs to check if you are allowed to play the card
    {
        if (fm.playerTurn == true)
        {
            if (player.hp == player.maxhp)
            {
                return false;
            }
            else if (player.hp + amount >= player.maxhp)
            {
                player.hp = player.maxhp;
                player.healthBars.updatePlayerBar();
                return true;
            }
            else
            {
                player.hp = player.hp + amount;
                player.healthBars.updatePlayerBar();
                return true;
            }
        }
        else if (fm.playerTurn == false)
        {
            if (enemy.hp == enemy.maxhp)
            {
                return false;
            }
            else if (enemy.hp + amount >= enemy.maxhp)
            {
                enemy.hp = enemy.maxhp;
                return true;
            }
            else
            {
                enemy.hp = enemy.hp + amount;
                enemy.healthBars.updateEnemyBar();
                return true;
            }
        }
        else { return false; }
    }

    // rerolls all dice
    bool Reroll(int numberOfDice)
    {
        diceRoller.allowRerolls(numberOfDice, false);
        //probably doesn't need to check for player or enemy turn?
        for (int i = 0; i < numberOfDice; i++)
        {
            diceRoller.ReRoll(i);
        }
        return true; //always returns true b/c I see no reason why it cant always reroll? AP is checked elsewhere
    }

    bool GrantFullHouse()
    {
        diceRoller.SetDiceValue(0, 3);
        diceRoller.SetDiceValue(1, 3);
        diceRoller.SetDiceValue(2, 3);
        diceRoller.SetDiceValue(3, 2);
        diceRoller.SetDiceValue(4, 2);
        return true;
    }
    bool ChangeDiceFace(int numberOfDice)
    {
        for (int i = 0; i < numberOfDice; i++)
        {
            diceRoller.SetDiceValue(i, 3);
        }
        return true;
    }
    bool ActivateShield(int amt, int numTurns)
    {
        fm.ActivateShield(amt, numTurns);
        return true;
    }

    bool ChangeMaxAP(int amount)
    {
        if (fm.playerTurn)
        {
            if (amount + player.maxActionPoints <= 10)
            {
                player.maxActionPoints++;
                fm.UpdateActionPoints();
                Debug.Log("Action Point Slot Added");
                return true;
            }
            else
            {
                Debug.Log("Cannot add any more AP! Max AP is 10!");
                return false;
            }
        }
        else
        {
            if (amount + enemy.maxEnemyActionPoints <= 10)
            {
                enemy.maxEnemyActionPoints++;
                fm.UpdateActionPoints();
                Debug.Log("Enemy Action Point Slot Added");
                return true;
            }
            else
            {
                Debug.Log("Cannot add any more AP! Max AP is 10!");
                return false;
            }
        }
    }

    bool DoubleDamage()
    {
        {
            if (fm.CalculateDamage() > 0)
            {
                fm.doubleDamage = true;
                return true;
            }
            else
            {
                Debug.Log("No outgoing damage! Card cannot be played!");
                return false;
            }
        }

    }

    bool ChooseReroll(int numDie, bool allowSameRerolls)
    {
        diceRoller.allowRerolls(numDie, allowSameRerolls);
        return true;
    }

    bool EnergyDrain(int num)
    {
        if (enemy.enemyActionPoints > 0)
        {
            enemy.enemyActionPoints = enemy.enemyActionPoints - num;
            fm.UpdateActionPoints();
            return true;
        }
        else
        {
            Debug.Log("Cannot Drain AP. Target has no AP!");
            return false;
        }
    }

    bool NoEffect()
    {
        return true;
    }

    //CARD ANIMATION FUNCTIONS//


    //ALL DRAG/UI FUNCTIONS//
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (playerCard)
        {
            HideTooltip();
        }
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (playerCard)
        {
            Vector3 worldPoint;
            RectTransform canvasRectTransform = GetComponentInParent<Canvas>().GetComponent<RectTransform>();

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out worldPoint))
            {
                transform.position = worldPoint;
            }
            HideTooltip();
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (playerCard)
        {
            if (IsPointerOverUIObject(eventData, fm.playRectTransform))
            {
                PlayCard(apCost);
            }
            else if (IsPointerOverUIObject(eventData, fm.burnRectTransform))
            {
                BurnCard();
            }
            /*else if(IsPointerOverUIObject(eventData, fm.upgradeRectTransform))
            {
                UpgradeCard(upgradable, cardID);
            }*/
            else
            {
                ShowTooltip(effectText);
            }
        }
    }
    private bool IsPointerOverUIObject(PointerEventData eventData, RectTransform target)
    {
        Vector2 localMousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target,
            eventData.position,
            eventData.pressEventCamera,
            out localMousePosition);

        return target.rect.Contains(localMousePosition);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        //Debug.Log("Mouse entered card area");
        ShowTooltip(effectText);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        //Debug.Log("Mouse exited card area");
        HideTooltip();
    }

    //ALL TOOLTIP FUNCTIONS//
    void ShowTooltip(string message)
    {
        if (playerCard)
        {
            //Debug.Log($"Showing tooltip with message: {message}");
            tooltipText.text = message;
            tooltipPanel.SetActive(true);
            isTooltipActive = true;
            UpdateTooltipPosition();
        }
    }

    void HideTooltip()
    {

        if (playerCard)
        {
            //tooltipPanel.SetActive(false);
            tooltipPanel.transform.position = offScreenPosition;
            isTooltipActive = false;
        }
    }
    void UpdateTooltipPosition()
    {
        if (playerCard)
        {
            // Calculate the position of the tooltip in world space relative to the card's position
            Vector3 newPos = transform.position + new Vector3(tooltipOffsetX, tooltipOffsetY, 0f);

            // Update the tooltip position directly in world space
            tooltipPanel.transform.position = newPos;
        }
    }
}
