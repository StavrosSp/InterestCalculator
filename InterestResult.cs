using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterestCalculator
{
    class InterestResult
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Days => (To - From).Days + 1;
        public decimal DikaiopraktikosRate { get; set; }
        public decimal DikaiopraktikosInterest { get; set; }
        public decimal IperimeriasRate { get; set; }
        public decimal IperimeriasInterest { get; set; }
    }
}
