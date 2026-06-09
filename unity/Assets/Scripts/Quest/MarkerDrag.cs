using UnityEngine;
using UnityEngine.EventSystems;

// Drag behaviour for a board marker. Because the marker GameObject is NOT tagged
// Game.BOARD, hovering it disables the camera pan/zoom (see CameraController.ScrollEnabled),
// so dragging a marker never moves the board. Right click removes the marker.
public class MarkerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
{
    public Marker marker;

    // Offset between the grab point and the marker centre, in board units
    private Vector2 grabOffset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
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
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            marker.Remove();
        }
    }
}
