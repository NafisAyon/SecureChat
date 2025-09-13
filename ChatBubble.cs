using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace Chatting
{
    internal class ChatBubble : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Message { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsMe { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int MaxWidth { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Bubble color
            Color bubbleColor = IsMe ? Color.LightBlue : Color.LightGray;
            using (Brush b = new SolidBrush(bubbleColor))
            {
                var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                int radius = 15;
                using (GraphicsPath path = RoundedRect(rect, radius))
                {
                    g.FillPath(b, path);
                }
            }

            // Draw text
            TextRenderer.DrawText(g, Message, Font, new Rectangle(10, 5, Width - 20, Height - 10), Color.Black);
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
