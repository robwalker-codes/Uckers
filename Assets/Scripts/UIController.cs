using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Button RollButton { get; private set; }
    public Text StatusText { get; private set; }
    public Text TurnText { get; private set; }

    private Action onRollClicked;

    public void Build(Action rollAction)
    {
        onRollClicked = rollAction;
        var canvasGo = new GameObject("Canvas");
        int uiLayer = LayerMask.NameToLayer("UI");
        canvasGo.layer = uiLayer >= 0 ? uiLayer : 5;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.transform.SetParent(transform, false);

        EnsureEventSystem();

        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        TurnText = CreateText(canvas.transform, "TurnText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), font, 26, TextAnchor.MiddleCenter);
        StatusText = CreateText(canvas.transform, "StatusText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), font, 20, TextAnchor.MiddleCenter);

        RollButton = CreateButton(canvas.transform, "RollButton", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-140f, 60f), new Vector2(160f, 50f), font, "Roll");
        RollButton.onClick.AddListener(() => onRollClicked?.Invoke());

        var controls = CreateText(canvas.transform, "Controls", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 80f), font, 16, TextAnchor.LowerLeft);
        controls.text = "Space: Roll  •  Click: Cycle  •  Enter: Confirm  •  R: Restart";
    }

    public void SetRollEnabled(bool enabled)
    {
        if (RollButton != null)
        {
            RollButton.interactable = enabled;
        }
    }

    public void SetStatus(string message)
    {
        if (StatusText != null)
        {
            StatusText.text = message;
        }
    }

    public void SetTurn(string message)
    {
        if (TurnText != null)
        {
            TurnText.text = message;
        }
    }

    public void SetRollLabel(string label)
    {
        if (RollButton != null)
        {
            RollButton.GetComponentInChildren<Text>().text = label;
        }
    }

    private Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Font font, int size, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
       rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
        {
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.sizeDelta = new Vector2(600f, 40f);
        }

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Font font, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        var button = go.AddComponent<Button>();

        var text = CreateText(go.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, font, 20, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = Color.white;

        return button;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        go.transform.SetParent(transform, false);
    }
}
