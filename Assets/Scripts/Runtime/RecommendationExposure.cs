using System;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Creating an offscreen row is not an exposure. Report once when at least
    // half the row intersects its scroll viewport, or the user selects it.
    public sealed class RecommendationExposure : MonoBehaviour
    {
        public Action OnVisible;
        bool reported;
        readonly Vector3[] corners=new Vector3[4];
        public void ReportVisible()
        {
            if(reported || OnVisible==null)return;
            reported=true;OnVisible();
        }
        void LateUpdate()
        {
            if(reported || OnVisible==null || !gameObject.activeInHierarchy)return;
            var scroll=GetComponentInParent<ScrollRect>();
            var viewport=scroll?.viewport;
            var rect=transform as RectTransform;
            if(viewport==null || rect==null || rect.rect.height<=0)return;
            rect.GetWorldCorners(corners);
            var bottom=viewport.InverseTransformPoint(corners[0]);
            var top=viewport.InverseTransformPoint(corners[2]);
            float height=top.y-bottom.y;
            float visible=Mathf.Max(0,Mathf.Min(top.y,viewport.rect.yMax)-Mathf.Max(bottom.y,viewport.rect.yMin));
            if(height>0 && visible/height>=.5f && top.x>viewport.rect.xMin && bottom.x<viewport.rect.xMax)ReportVisible();
        }
    }
}
