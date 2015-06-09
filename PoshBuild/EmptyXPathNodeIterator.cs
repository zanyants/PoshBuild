using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;

namespace PoshBuild
{
    sealed class EmptyXPathNodeIterator : XPathNodeIterator
    {
        public override XPathNodeIterator Clone()
        {
            return new EmptyXPathNodeIterator();
        }

        public override XPathNavigator Current
        {
            get { return null; }
        }

        public override int CurrentPosition
        {
            get { return 0; }
        }

        public override bool MoveNext()
        {
            return false;
        }
    }
}
