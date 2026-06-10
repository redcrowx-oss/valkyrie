using UnityEngine;
using UnityEngine.EventSystems;
using Assets.Scripts.Content;
using Assets.Scripts.UI;

// Drag behaviour for a board marker. Because the marker GameObject is NOT tagged
// Game.BOARD, hovering it disables the camera pan/zoom (see CameraController.ScrollEnabled),
// so dragging a marker never moves the board.
//
// Removing a marker:
//  - Right click (mouse / desktop / editor).
//  - Double tap (touch). Two taps on the SAME marker within DoubleTapWindow seconds,
//    close together on screen, with NO drag in between. A drag clears the pending
//    first tap, so nudging a marker into place never deletes it by accident.
// Both paths ask for confirmation first, since removal is destructive.
public class MarkerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
{
    public Marker marker;

    // Offset between the grab point and the marker centre, in board units
    private Vector2 grabOffset;

    // Double-tap detection (touch). Window matches the EventSystem double-click time.
    private const float DoubleTapWindow = 0.3f;
    private float lastTapTime = -1f;
    private Vector2 lastTapPosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        // A drag invalidates any pending first tap, so tap-drag-tap never deletes.
        lastTapTime = -1f;
        Vector2 mouse = Game.Get().cc.GetMouseBoardPlane();
        grabOffset = new Vector2(transform.position.x, transform.position.y) - mouse;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Vector2 target = Game.Get().cc.GetMouseBoardPlane() + grabOffset;
        marker.SetPosition(target.x, target.y);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Mouse: right click removes directly.
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ConfirmRemove();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Touch / left click: detect a double tap. OnPointerClick only fires for
        // taps that were NOT drags (a drag clears eligibleForClick), so a tap here
        // already means "no drag movement" for this press.
        float now = Time.unscaledTime;
        // Same-spot tolerance, relative to screen size so it scales across devices.
        float moveTolerance = Screen.height * 0.05f;
        if (lastTapTime >= 0f
            && now - lastTapTime <= DoubleTapWindow
            && Vector2.Distance(eventData.position, lastTapPosition) <= moveTolerance)
        {
            lastTapTime = -1f;
            ConfirmRemove();
        }
        else
        {
            lastTapTime = now;
            lastTapPosition = eventData.position;
        }
    }

    // Quick "Confirm / Delete / Cancel" modal before the destructive removal.
    private void ConfirmRemove()
    {
        UIElement ui = new UIElement();
        ui.SetLocation(UIScaler.GetHCenter(-5f), UIScaler.GetVCenter(-2.5f), 10f, 5f);
        new UIElementBorder(ui);

        ui = new UIElement();
        ui.SetLocation(UIScaler.GetHCenter(-4.5f), UIScaler.GetVCenter(-2f), 9f, 1.5f);
        ui.SetText(CommonStringKeys.CONFIRM);

        // Delete (destructive, red)
        ui = new UIElement();
        ui.SetLocation(UIScaler.GetHCenter(-4.5f), UIScaler.GetVCenter(0f), 4f, 1.5f);
        ui.SetText(CommonStringKeys.DELETE, Color.red);
        ui.SetButton(delegate { Destroyer.Dialog(); marker.Remove(); });
        ui.SetBGColor(new Color(0.0f, 0.03f, 0f));
        new UIElementBorder(ui, Color.red);

        // Cancel
        ui = new UIElement();
        ui.SetLocation(UIScaler.GetHCenter(0.5f), UIScaler.GetVCenter(0f), 4f, 1.5f);
        ui.SetText(CommonStringKeys.CANCEL);
        ui.SetButton(delegate { Destroyer.Dialog(); });
        ui.SetBGColor(new Color(0.03f, 0, 0f));
        new UIElementBorder(ui);
    }
}
