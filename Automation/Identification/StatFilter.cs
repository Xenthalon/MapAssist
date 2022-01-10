using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation.Identification
{
    public enum StatFilterType
    {
        EQ, LT, GT, LTE, GTE, NONE
    }

    public class StatFilter
    {
        public Stat Stat { get; set; }
        public StatFilterType StatFilterType { get; set; }
        public int Value { get; set; }
    }
}
