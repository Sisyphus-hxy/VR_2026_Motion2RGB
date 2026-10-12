using UnityEngine;
using UnityEngine.UI;

namespace Motion2RGB
{
    /// <summary>
    /// Small allocation-free UGUI plot for the three live motion signals.
    /// It is intentionally a normal Graphic so it works in the Built-in pipeline.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MotionTelemetryGraph : Graphic
    {
        public MotionStateTracker tracker;
        [Min(16)] public int sampleCount = 120;
        [Min(0.1f)] public float maxSpeed = 4f;
        [Min(1f)] public float maxAngularVelocity = 180f;
        [Min(0.1f)] public float maxGaitFrequency = 5f;
        [Min(0.5f)] public float lineWidth = 2f;
        public Color speedColor = new Color(0.25f, 0.9f, 1f, 1f);
        public Color angularColor = new Color(1f, 0.7f, 0.25f, 1f);
        public Color gaitColor = new Color(0.45f, 1f, 0.45f, 1f);
        [Min(0.01f)] public float sampleInterval = 0.08f;

        private float[] speedSamples;
        private float[] angularSamples;
        private float[] gaitSamples;
        private int writeIndex;
        private float sampleTimer;

        protected override void Awake()
        {
            base.Awake();
            AllocateBuffers();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AllocateBuffers();
            SetVerticesDirty();
        }

        private void Update()
        {
            if (tracker == null) return;
            sampleTimer += Time.unscaledDeltaTime;
            if (sampleTimer < sampleInterval) return;
            sampleTimer = 0f;
            speedSamples[writeIndex] = Mathf.Clamp01(tracker.PlanarSpeed / Mathf.Max(0.01f, maxSpeed));
            angularSamples[writeIndex] = Mathf.Clamp01(tracker.AngularVelocityDegPerSec / Mathf.Max(1f, maxAngularVelocity));
            gaitSamples[writeIndex] = Mathf.Clamp01(tracker.GaitFrequencyHz / Mathf.Max(0.01f, maxGaitFrequency));
            writeIndex = (writeIndex + 1) % speedSamples.Length;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (speedSamples == null || speedSamples.Length < 2) return;
            Rect rect = rectTransform.rect;
            AddGrid(vh, rect);
            AddSeries(vh, rect, speedSamples, speedColor);
            AddSeries(vh, rect, angularSamples, angularColor);
            AddSeries(vh, rect, gaitSamples, gaitColor);
        }

        private void AddGrid(VertexHelper vh, Rect rect)
        {
            Color grid = new Color(1f, 1f, 1f, 0.1f);
            AddLine(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), grid, 1f);
            AddLine(vh, new Vector2(rect.xMin, rect.center.y), new Vector2(rect.xMax, rect.center.y), grid, 1f);
            AddLine(vh, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), grid, 1f);
        }

        private void AddSeries(VertexHelper vh, Rect rect, float[] samples, Color color)
        {
            Vector2 previous = SamplePoint(rect, samples, 0);
            for (int i = 1; i < samples.Length; i++)
            {
                Vector2 current = SamplePoint(rect, samples, i);
                AddLine(vh, previous, current, color, lineWidth);
                previous = current;
            }
        }

        private Vector2 SamplePoint(Rect rect, float[] samples, int index)
        {
            int chronological = (writeIndex + index) % samples.Length;
            float x = Mathf.Lerp(rect.xMin, rect.xMax, index / (float)(samples.Length - 1));
            float y = Mathf.Lerp(rect.yMin, rect.yMax, samples[chronological]);
            return new Vector2(x, y);
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < 0.000001f) return;
            Vector2 normal = direction.normalized * (width * 0.5f);
            normal = new Vector2(-normal.y, normal.x);
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AllocateBuffers()
        {
            int count = Mathf.Max(16, sampleCount);
            if (speedSamples != null && speedSamples.Length == count) return;
            speedSamples = new float[count];
            angularSamples = new float[count];
            gaitSamples = new float[count];
            writeIndex = 0;
        }
    }
}
