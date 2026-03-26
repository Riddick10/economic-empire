using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class NewsTopMenuPanel : ITopMenuPanel
{
    public string Title => "NACHRICHTEN";
    public TopMenuPanel PanelType => TopMenuPanel.News;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("NACHRICHTEN");
        var (panelX, panelY, panelW, panelH) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        int contentW = panelW - 30;

        var notifMgr = Program.game.GetSystem<NotificationManager>();
        if (notifMgr == null) return;

        notifMgr.MarkAllAsRead();

        var notifications = notifMgr.Notifications;

        if (notifications.Count == 0)
        {
            Program.DrawGameText("Keine Nachrichten", contentX, y + 20, 14, ColorPalette.TextGray);
            return;
        }

        int visibleHeight = panelY + panelH - y - 10;
        int itemHeight = 85;
        int totalHeight = notifications.Count * itemHeight;
        int maxScroll = Math.Max(0, totalHeight - visibleHeight);

        Vector2 mousePos = Program._cachedMousePos;
        Rectangle panelRect = new Rectangle(panelX, panelY, panelW, panelH);
        if (Raylib.CheckCollisionPointRec(mousePos, panelRect))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                Program.ui.NewsScrollOffset -= (int)(wheel * 40);
                Program.ui.NewsScrollOffset = Math.Clamp(Program.ui.NewsScrollOffset, 0, maxScroll);
            }
        }

        Raylib.BeginScissorMode(panelX + 1, y, panelW - 2, visibleHeight);

        int drawY = y - Program.ui.NewsScrollOffset;
        int? clickedNotificationId = null;

        foreach (var notification in notifications)
        {
            if (drawY + itemHeight < y || drawY > panelY + panelH)
            {
                drawY += itemHeight;
                continue;
            }

            Rectangle itemRect = new Rectangle(contentX, drawY + 2, contentW, itemHeight - 4);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, itemRect) &&
                             mousePos.Y >= y && mousePos.Y <= panelY + panelH - 10;

            if (isHovered)
            {
                Raylib.DrawRectangle(contentX, drawY + 2, contentW, itemHeight - 4,
                    new Color((byte)50, (byte)50, (byte)70, (byte)150));

                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    clickedNotificationId = notification.Id;
                }
            }

            Color typeColor = notification.Type switch
            {
                NotificationType.Info => ColorPalette.Accent,
                NotificationType.Warning => ColorPalette.Yellow,
                NotificationType.Danger => ColorPalette.Red,
                NotificationType.Success => ColorPalette.Green,
                _ => ColorPalette.TextGray
            };

            Raylib.DrawRectangle(contentX, drawY + 2, 4, itemHeight - 8, typeColor);

            Program.DrawGameText(notification.DateString, contentX + 12, drawY + 6, 14, ColorPalette.TextGray);

            if (notification.RelatedCountryId != null)
            {
                Program.DrawFlag(notification.RelatedCountryId, contentX + contentW - 30, drawY + 4, 16);
            }

            Color titleColor = isHovered ? ColorPalette.TextWhite : ColorPalette.Accent;
            Program.DrawGameText(notification.Title, contentX + 12, drawY + 26, 14, titleColor);

            string msg = notification.Message;
            int maxTextW = contentW - 24;
            while (Program.MeasureTextCached(msg, 14) > maxTextW && msg.Length > 3)
            {
                msg = msg[..^4] + "...";
            }
            Program.DrawGameText(msg, contentX + 12, drawY + 50, 14, ColorPalette.TextGray);

            if (isHovered)
            {
                string hint = "Klicken zum Lesen";
                int hintW = Program.MeasureTextCached(hint, 11);
                Program.DrawGameText(hint, contentX + contentW - hintW - 10, drawY + itemHeight - 18, 14, ColorPalette.Accent);
            }

            Raylib.DrawLine(contentX + 12, drawY + itemHeight - 4,
                contentX + contentW, drawY + itemHeight - 4,
                new Color((byte)60, (byte)60, (byte)80, (byte)100));

            drawY += itemHeight;
        }

        Raylib.EndScissorMode();

        if (clickedNotificationId.HasValue)
        {
            notifMgr.ReshowAsPopup(clickedNotificationId.Value);
            Program.ui.ActiveTopMenuPanel = TopMenuPanel.None;
        }

        if (totalHeight > visibleHeight)
        {
            int scrollBarH = Math.Max(20, visibleHeight * visibleHeight / totalHeight);
            int scrollBarY = y + (int)((float)Program.ui.NewsScrollOffset / maxScroll * (visibleHeight - scrollBarH));
            int scrollBarX = panelX + panelW - 6;
            Raylib.DrawRectangle(scrollBarX, scrollBarY, 4, scrollBarH, ColorPalette.PanelLight);
        }
    }
}
