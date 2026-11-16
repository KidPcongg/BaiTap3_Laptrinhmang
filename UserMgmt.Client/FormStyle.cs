namespace UserMgmt.Client;

internal static class FormStyle
{
    internal static readonly Color Teal = Color.FromArgb(18, 117, 121);
    internal static readonly Color Pale = Color.FromArgb(236, 245, 246);
    internal static readonly Color Error = Color.FromArgb(190, 58, 58);

    internal static void Apply(Form form)
    {
        form.Font = new Font("Segoe UI", 10F);
        form.BackColor = Color.White;
        form.ForeColor = Color.FromArgb(48, 63, 66);
        form.FormBorderStyle = FormBorderStyle.FixedSingle;
        form.MaximizeBox = false;
        form.StartPosition = FormStartPosition.CenterScreen;
        foreach (Control control in form.Controls)
            ApplyControl(control);
    }

    private static void ApplyControl(Control control)
    {
        if (control is TextBox text)
        {
            text.BorderStyle = BorderStyle.FixedSingle;
            text.BackColor = Color.FromArgb(247, 251, 251);
        }
        if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 218, 220);
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            if (button.Tag as string == "primary")
            {
                button.BackColor = Teal;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Teal;
            }
            else
            {
                button.BackColor = Pale;
                button.ForeColor = Color.FromArgb(48, 63, 66);
            }
        }
        if (control is LinkLabel link)
        {
            link.LinkColor = Teal;
            link.ActiveLinkColor = Teal;
            link.VisitedLinkColor = Teal;
        }
        foreach (Control child in control.Controls)
            ApplyControl(child);
    }
}
