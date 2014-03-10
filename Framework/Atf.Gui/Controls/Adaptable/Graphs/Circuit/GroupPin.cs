﻿//Copyright © 2014 Sony Computer Entertainment America LLC. See License.txt.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

using Sce.Atf.Adaptation;
using Sce.Atf.Dom;
using Sce.Atf.Rendering;


namespace Sce.Atf.Controls.Adaptable.Graphs
{
    /// <summary>
    /// A pin on a group module, with extra information needed to associate the pin
    /// on the group with the internal module where it was connected before grouping</summary>
    public abstract class GroupPin : Pin, ICircuitGroupPin<Element>, IVisible
    {
        protected abstract AttributeInfo IndexAttribute { get; }

        protected abstract AttributeInfo PinYAttribute { get; }

        protected abstract AttributeInfo ElementAttribute { get; }

        protected abstract AttributeInfo PinAttribute { get; }

        protected abstract AttributeInfo PinnedAttribute { get; }

        protected abstract AttributeInfo VisibleAttribute { get; }
     
        public GroupPin()
        {
            m_info = new CircuitGroupPinInfo();
            m_info.Changed += InfoChanged;
            m_isDefaultName = true;
        }

        /// <summary>
        /// Gets index of this group pin in the owning group’s input/output group pin list</summary>
        public override int Index
        {
            get { return GetAttribute<int>(IndexAttribute); }
            set { SetAttribute(IndexAttribute, value); }
        }

        /// <summary>
        /// Gets whether connection fan in to pin is allowed</summary>
        public override bool AllowFanIn
        {
            get
            {
                ICircuitPin pin = IsInputSide ? PinTarget.LeafDomNode.Cast<ICircuitElement>().Type.Inputs[PinTarget.LeafPinIndex] :
                                                PinTarget.LeafDomNode.Cast<ICircuitElement>().Type.Outputs[PinTarget.LeafPinIndex];
                return pin.AllowFanIn;
            }
        }

        /// <summary>
        /// Gets whether connection fan out from pin is allowed</summary>
        public override bool AllowFanOut
        {
            get
            {
                ICircuitPin pin = IsInputSide ? PinTarget.LeafDomNode.Cast<ICircuitElement>().Type.Inputs[PinTarget.LeafPinIndex] :
                                                PinTarget.LeafDomNode.Cast<ICircuitElement>().Type.Outputs[PinTarget.LeafPinIndex];
                return pin.AllowFanOut;
            }
        }

        /// <summary>
        /// Gets or sets the local position of the group pin</summary>
        public virtual Point Position
        {
            get { return new Point(0, GetAttribute<int>(PinYAttribute)); }
            set { SetAttribute(PinYAttribute, value.Y); }
        }

        /// <summary>
        /// Gets the default group pin name</summary>
        /// <remarks>The group pin default naming convention:  internal-element-name: internal-pin-name</remarks>
        public virtual string DefaultName(bool inputSide)
        {
            string pinName;
            if (InternalElement.Type.Is<Group>())
            {
                var group = InternalElement.Type.Cast<Group>();
                var groupPin = inputSide
                                   ? group.InputGroupPins.First(n => n.Index == InternalPinIndex)
                                   : group.OutputGroupPins.First(n => n.Index == InternalPinIndex);
                pinName = groupPin.Name;
            }
            else
                pinName = inputSide
                            ? InternalElement.Type.Inputs[InternalPinIndex].Name
                            : InternalElement.Type.Outputs[InternalPinIndex].Name;
                   
            return InternalElement.Name + ":" + pinName;
        }

        /// <summary>
        /// Gets/sets a value indicating whether the current group pin name is the default value.</summary>
        public bool IsDefaultName
        {
            get { return m_isDefaultName;  }
            set { m_isDefaultName = value; }
        }

        /// <summary>
        /// Gets or sets whether the group pin is 'pinned'. When pinned, the pin will not be automatically
        /// relocated when the internal element that the pin is connected to is relocated.</summary>
        public bool Pinned
        {
            get { return GetAttribute<bool>(PinnedAttribute); }
            set { SetAttribute(PinnedAttribute, value); }
        }

        /// <summary>
        /// Gets or sets whether the element is visible</summary>
        public bool Visible
        {
            get { return GetAttribute<bool>(VisibleAttribute); }
            set { SetAttribute(VisibleAttribute, value); }
        }

        #region ICircuitGroupPin members

        /// <summary>
        /// Gets the circuit element within this group that this group pin connects to</summary>
        public Element InternalElement
        {
            get { return GetReference<Element>(ElementAttribute); }
            set { SetReference(ElementAttribute, value); }
        }

        /// <summary>
        /// Gets the index of the pin on InternalElement that this group pin connects to</summary>
        public int InternalPinIndex
        {
            get { return GetAttribute<int>(PinAttribute); }
            set { DomNode.SetAttribute(PinAttribute, value); }
        }

        /// <summary>
        /// Gets or sets the bounding rectangle for the group pin in local space</summary>
        public virtual Rectangle Bounds
        {
            get
            {
                return new Rectangle(Position, m_size);
            }
            set
            {
                Position = value.Location;
                m_size = value.Size;
            }
        }

        /// <summary>
        /// Gets or sets the CircuitGroupInfo object which controls various options on this circuit
        /// group pin. Is never null.</summary>
        public CircuitGroupPinInfo Info
        {
            get { return m_info; }
            set { m_info = value; }
        }

        #endregion


        /// <summary>
        /// Gets or sets the pin target.</summary>
        public PinTarget PinTarget
        {
            get
            {
                Debug.Assert(m_pinTarget != null);
                return m_pinTarget;
            }
            set { m_pinTarget = value; }
        }


        /// <summary>
        /// Update LeafDomNode & LeafPinIndex from the ultimate DomNode this group pin binds to
        /// by recursively going down the nested group hierarchy
        /// </summary>
        /// <param name="inputSide">true if this is a input-side group pin</param>
        public void SetPinTarget(bool inputSide)
        {
            m_inputSide = inputSide;

            DomNode instancingNode;
            DomNode leafNode = GetLeafDomNode(inputSide, out instancingNode);
            if (m_pinTarget != null && m_pinTarget.InstancingNode!= null)
                instancingNode = m_pinTarget.InstancingNode;// try keep the original instancingNode

            PinTarget = new PinTarget(leafNode, GetLeafPinIndex(inputSide), instancingNode);
        }

        /// <summary>
        /// Gets a value that indicates whether this group pin is for input side.
        /// </summary>
        public bool IsInputSide
        {
            get
            {
                Debug.Assert(PinTarget != null);
                return m_inputSide;
            }
        }
        /// <summary>
        /// Returns the pins down the chain before the leaf level. </summary>
        /// <param name="inputSide">true if this is a input-side group pin</param>
        public IEnumerable<GroupPin> SinkChain(bool inputSide)
        {
            yield return this;
            var current = this;
            while (current.InternalElement.Is<Group>())
            {     
                // go down
                var childSubGraph = current.InternalElement.Cast<Group>();
                current = inputSide ? childSubGraph.InputGroupPins.First(x => x.PinTarget == PinTarget) :
                      childSubGraph.OutputGroupPins.First(x => x.PinTarget == PinTarget);
                yield return current;

            } 
        }

        /// <summary>
        /// Gets the lineage of this group pin, starting with the group pin, and ending with
        /// the top-level group pin.</summary>
        private IEnumerable<GroupPin> GetLineage(bool inputSide)
        {
            GroupPin grpPin = this;
            DomNode domNode = DomNode;
            while (grpPin != null)
            {
                yield return grpPin;
                GroupPin nextGrpPin = null;

                var subGraph = domNode.Parent.Cast<Group>();

                if (subGraph.ParentGraph.Is<Group>())
                {
                    var parentSubGraph = subGraph.ParentGraph.Cast<Group>();

                    if (parentSubGraph != null)
                    {
                        var parentPins = inputSide ? parentSubGraph.InputGroupPins : parentSubGraph.OutputGroupPins;
                        foreach (var parentPin in parentPins)
                        {
                            if (parentPin.InternalElement == subGraph && parentPin.InternalPinIndex == grpPin.Index)
                            {
                                nextGrpPin = parentPin;
                                domNode = nextGrpPin.DomNode;
                                break;
                            }
                        }
                    }
                }

                grpPin = nextGrpPin;
            }

        }

        /// <summary>
        /// Gets the ancestry of this group pin, starting with the parent group pin, and ending with
        /// the top-level group pin.</summary>
        public IEnumerable<GroupPin> GetAncestry(bool inputSide)
        {
            bool firstTime = true;
            foreach (var grpPin in GetLineage(inputSide))
            {
                if (firstTime)
                {
                    firstTime = false;
                    continue;
                }
                yield return grpPin;
            }
        }

        protected override void OnNodeSet()
        {
            DomNode.AttributeChanged += DomNode_AttributeChanged;
            base.OnNodeSet();

            // We must avoid getting called back when setting m_info.Pinned, to avoid corrupting VisibleAttribute.
            m_infoBeingSynced = true;
            try
            {
                m_info.Pinned = GetAttribute<bool>(PinnedAttribute);
                m_info.Visible = GetAttribute<bool>(VisibleAttribute);
            }
            finally
            {
                m_infoBeingSynced = false;
            }

            var group = GetParentAs<Group>();
            if (group != null)
            {
                bool inputSide = group.InputGroupPins.Contains(this);
                IsDefaultName = Name == DefaultName(inputSide);
            }
        }

        // Desired position
        public Point DesiredLocation { get; set; }

        // complements InfoChanged()
        void DomNode_AttributeChanged(object sender, AttributeEventArgs e)
        {
            if (sender == DomNode)
            {
                if (!m_infoBeingSynced)
                {
                    try
                    {
                        m_infoBeingSynced = true;
                        if (e.AttributeInfo == PinnedAttribute)
                            m_info.Pinned = (bool) e.NewValue;
                        else  if (e.AttributeInfo == VisibleAttribute)
                            m_info.Visible = (bool)e.NewValue;
                    }
                    finally
                    {
                        m_infoBeingSynced = false;
                    }
                }
            }
        }

        // complements DomNode_AttributeChanged()
        private void InfoChanged(object sender, EventArgs e)
        {
            if (!m_infoBeingSynced)
            {
                try
                {
                    m_infoBeingSynced = true;
                    SetAttribute(PinnedAttribute, m_info.Pinned);
                    SetAttribute(VisibleAttribute, m_info.Visible);
                }
                finally
                {
                    m_infoBeingSynced = false;
                }
            }
        }


        /// <summary>
        /// Returns the ultimate DomNode this group pin binds by recursively going down the nested group hierarchy.</summary>
        /// <param name="inputSide">true if this is a input-side group pin</param>
        /// <param name="instancingNode">for template reference instances, the owner node</param>
        private DomNode GetLeafDomNode(bool inputSide, out DomNode instancingNode)
        {
            var current = this;
            instancingNode = null;
            while (current.InternalElement.Is<Group>()) // traverse down
            {         
                if (current.InternalElement.Is<IReference<DomNode>>())
                {
                    if (instancingNode == null)
                        instancingNode = current.InternalElement.DomNode;
                }

                var childSubGraph = current.InternalElement.Cast<Group>();
                current = inputSide ? childSubGraph.InputGroupPins.First(x => x.Index == current.InternalPinIndex) :
                      childSubGraph.OutputGroupPins.First(x => x.Index == current.InternalPinIndex);               
            }

            if ( current.InternalElement.Is<IReference<DomNode>>()) // case for plain node references
            {
                if (instancingNode == null)
                    instancingNode = current.InternalElement.DomNode;
                var reference = current.InternalElement.As<IReference<DomNode>>();
                return reference.Target; //dereference
            }
            return current.InternalElement.DomNode;
        }

        /// <summary>
        /// Returns the ultimate pin index (in the binding module) this group pin binds by recursively
        /// going down the nested group hierarchy</summary>
        /// <param name="inputSide">true if this is a input-side group pin</param>
        private int GetLeafPinIndex(bool inputSide)
        {
            var current = this;
            while (current.InternalElement.Is<Group>())
            {
                var childSubGraph = current.InternalElement.Cast<Group>();
                current = inputSide ? childSubGraph.InputGroupPins.First(x => x.Index == current.InternalPinIndex) :
                      childSubGraph.OutputGroupPins.First(x => x.Index == current.InternalPinIndex);

            }
            return current.InternalPinIndex;
        }


        private PinTarget m_pinTarget;
        private Size m_size;
        private CircuitGroupPinInfo m_info;
        private bool m_infoBeingSynced;
        private bool m_isDefaultName;
        private bool m_inputSide;
    }
}
