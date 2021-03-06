using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;

namespace UnityEditor.ShaderGraph.Drawing
{
    class BlackboardSection : GraphElement, IDropTarget
    {
        private VisualElement m_DragIndicator;
        private VisualElement m_MainContainer;
        private VisualElement m_Header;
        private Label m_TitleLabel;
        private VisualElement m_RowsContainer;
        private int m_InsertIndex;

        int InsertionIndex(Vector2 pos)
        {
            int index = -1;
            VisualElement owner = contentContainer != null ? contentContainer : this;
            Vector2 localPos = this.ChangeCoordinatesTo(owner, pos);

            if (owner.ContainsPoint(localPos))
            {
                index = 0;

                foreach (VisualElement child in Children())
                {
                    Rect rect = child.layout;

                    if (localPos.y > (rect.y + rect.height / 2))
                    {
                        ++index;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return index;
        }

        VisualElement FindSectionDirectChild(VisualElement element)
        {
            VisualElement directChild = element;

            while ((directChild != null) && (directChild != this))
            {
                if (directChild.parent == this)
                {
                    return directChild;
                }
                directChild = directChild.parent;
            }

            return null;
        }

        public BlackboardSection()
        {
            var tpl = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UnityShaderEditor/Editor/Resources/UXML/GraphView/BlackboardSection.uxml") as VisualTreeAsset;
            m_MainContainer = tpl.CloneTree(null);
            m_MainContainer.AddToClassList("mainContainer");

            m_Header = m_MainContainer.Q<VisualElement>("sectionHeader");
            m_TitleLabel = m_MainContainer.Q<Label>("sectionTitleLabel");
            m_RowsContainer = m_MainContainer.Q<VisualElement>("rowsContainer");

            shadow.Add(m_MainContainer);

            m_DragIndicator = new VisualElement();

            m_DragIndicator.name = "dragIndicator";
            m_DragIndicator.style.positionType = PositionType.Absolute;
            shadow.Add(m_DragIndicator);

            ClearClassList();
            AddToClassList("sgblackboardSection");

            m_InsertIndex = -1;
        }

        public override VisualElement contentContainer { get { return m_RowsContainer; } }

        public string title
        {
            get { return m_TitleLabel.text; }
            set { m_TitleLabel.text = value; }
        }

        public bool headerVisible
        {
            get { return m_Header.parent != null; }
            set
            {
                if (value == (m_Header.parent != null))
                    return;

                if (value)
                {
                    m_MainContainer.Add(m_Header);
                }
                else
                {
                    m_MainContainer.Remove(m_Header);
                }
            }
        }

        private void SetDragIndicatorVisible(bool visible)
        {
            if (visible && (m_DragIndicator.parent == null))
            {
                shadow.Add(m_DragIndicator);
                m_DragIndicator.visible = true;
            }
            else if ((visible == false) && (m_DragIndicator.parent != null))
            {
                shadow.Remove(m_DragIndicator);
            }
        }

        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            // Look for at least one selected element in this section to accept drop
            foreach (ISelectable selected in selection)
            {
                VisualElement selectedElement = selected as VisualElement;

                if (selected != null && Contains(selectedElement))
                {
                    return true;
                }
            }

            return false;
        }

//        public bool DragUpdated(DragUpdatedEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget)
        public EventPropagation DragUpdated(IMGUIEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget)
        {
            VisualElement sourceItem = null;

            foreach (ISelectable selectedElement in selection)
            {
                sourceItem = selectedElement as VisualElement;

                if (sourceItem == null)
                    continue;
            }

            if (!Contains(sourceItem))
            {
                SetDragIndicatorVisible(false);

                return EventPropagation.Continue;
            }

            var target = evt.target as VisualElement;
//            Vector2 localPosition = target.ChangeCoordinatesTo(this, evt.localMousePosition);
            Vector2 localPosition = target.ChangeCoordinatesTo(this, evt.imguiEvent.mousePosition);

            m_InsertIndex = InsertionIndex(localPosition);

            if (m_InsertIndex != -1)
            {
                float indicatorY = 0;

                if (m_InsertIndex == childCount)
                {
                    VisualElement lastChild = this[childCount - 1];

                    indicatorY = lastChild.ChangeCoordinatesTo(this, new Vector2(0, lastChild.layout.height + lastChild.style.marginBottom)).y;
                }
                else
                {
                    VisualElement childAtInsertIndex = this[m_InsertIndex];

                    indicatorY = childAtInsertIndex.ChangeCoordinatesTo(this, new Vector2(0, -childAtInsertIndex.style.marginTop)).y;
                }

                SetDragIndicatorVisible(true);

                m_DragIndicator.layout = new Rect(0, indicatorY - m_DragIndicator.layout.height / 2, layout.width, m_DragIndicator.layout.height);
            }
            else
            {
                SetDragIndicatorVisible(false);

                m_InsertIndex = -1;
            }

            return EventPropagation.Stop;
        }

        int IndexOf(VisualElement element)
        {
            var index = 0;
            foreach (var childElement in Children())
            {
                if (childElement == element)
                    return index;
                index++;
            }
            return -1;
        }

        struct VisualElementPair
        {
            public VisualElement Item1;
            public VisualElement Item2;

            public VisualElementPair(VisualElement item1, VisualElement item2)
            {
                Item1 = item1;
                Item2 = item2;
            }
        }

//        public bool DragPerform(DragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget)
        public EventPropagation DragPerform(IMGUIEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget)
        {
            if (m_InsertIndex != -1)
            {
                List<VisualElementPair> draggedElements = new List<VisualElementPair>();

                foreach (ISelectable selectedElement in selection)
                {
                    var draggedElement = selectedElement as VisualElement;

                    if (draggedElement != null && Contains(draggedElement))
                    {
                        draggedElements.Add(new VisualElementPair(FindSectionDirectChild(draggedElement), draggedElement));
                    }
                }

                if (draggedElements.Count == 0)
                {
                    SetDragIndicatorVisible(false);

                    return EventPropagation.Continue;
                }

                // Sorts the dragged elements from their relative order in their parent
                draggedElements.Sort((pair1, pair2) => { return IndexOf(pair1.Item1).CompareTo(IndexOf(pair2.Item1)); });

                int insertIndex = m_InsertIndex;

                foreach (var draggedElement in draggedElements)
                {
                    VisualElement sectionDirectChild = draggedElement.Item1;
                    int indexOfDraggedElement = IndexOf(sectionDirectChild);

                    if (!((indexOfDraggedElement == insertIndex) || ((insertIndex - 1) == indexOfDraggedElement)))
                    {
                        Blackboard blackboard = GetFirstAncestorOfType<Blackboard>();

                        if (blackboard.moveItemRequested != null)
                        {
                            blackboard.moveItemRequested(blackboard, m_InsertIndex, draggedElement.Item2);
                        }
                        else
                        {
                            if (insertIndex == contentContainer.childCount)
                            {
                                sectionDirectChild.BringToFront();
                            }
                            else
                            {
                                sectionDirectChild.PlaceBehind(this[insertIndex]);
                            }
                        }
                    }

                    if (insertIndex > indexOfDraggedElement) // No need to increment the insert index for the next dragged element if the current dragged element is above the current insert location.
                        continue;
                    insertIndex++;
                }
            }

            SetDragIndicatorVisible(false);

            return EventPropagation.Stop;
        }

        EventPropagation IDropTarget.DragExited()
        {
            SetDragIndicatorVisible(false);
            return EventPropagation.Stop;
        }
    }
}
