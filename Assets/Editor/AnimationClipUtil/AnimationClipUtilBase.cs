using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationClipUtil
{
    public abstract class AnimationClipUtilBase
    {
        public AnimationClipUtilWindow window { get; private set; }

        public AnimationClipUtilBase(AnimationClipUtilWindow window)
        {
            this.window = window;
        }

        public abstract void Draw(bool refresh);
    }
}