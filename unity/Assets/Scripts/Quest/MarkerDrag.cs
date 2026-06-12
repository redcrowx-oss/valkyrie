using UnityEngine;
using UnityEngine.EventSystems;
using Assets.Scripts.Content;
using Assets.Scripts.UI;

// Drag behaviour for a board marker. Because the marker GameObject is NOT tagged
// Game.BOARD, hovering it disables the camera pan/zoom (see CameraController.ScrollEnabled),
// so dragging a marker never moves the board.
//
// Removing a marker (immediate — markers are cheap to re-place, so no confirmation):
//  - Right click (mouse / desktop / editor).
//  - Double tap (touch). Two taps on the SAME marker within DoubleTapWindow seconds,
//    close together on screen, with NO drag in between. A drag clears the pending
//    first tap, so nudging a marker into place never deletes it by accident.
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
            marker.Remove();
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
            marker.Remove();
        }
        else
        {
            lastTapTime = now;
            lastTapPosition = eventData.position;
        }
    }
}
