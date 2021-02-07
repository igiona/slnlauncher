using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slnx.Interfaces
{
    public interface IBranchable
    {
        string BranchableDirectory { get; }

        string FullPath { get; }

    }
}
