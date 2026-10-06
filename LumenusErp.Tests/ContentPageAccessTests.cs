using System.Security.Claims;
using LumenusErp.Data;
using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class ContentPageAccessTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private static ClaimsPrincipal User(string? id, params string[] roles)
    {
        var claims = new List<Claim>();
        if (id is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static readonly PageVisibility[] None = [];

    [Theory]
    [InlineData(null)]
    [InlineData(Me)]
    [InlineData(Other)]
    public void Admin_edits_any_page(string? owner) =>
        Assert.True(ContentPageAccess.CanEdit(User("admin-1", "Admin"), owner));

    [Fact]
    public void Creator_edits_own_page() =>
        Assert.True(ContentPageAccess.CanEdit(User(Me, "Creator"), Me));

    [Theory]
    [InlineData(Other)]
    [InlineData(null)]
    public void Creator_cannot_edit_foreign_or_ownerless_page(string? owner) =>
        Assert.False(ContentPageAccess.CanEdit(User(Me, "Creator"), owner));

    [Fact]
    public void Creator_without_user_id_cannot_edit()
    {
        var user = User(null, "Creator");
        Assert.False(ContentPageAccess.CanEdit(user, null));
        Assert.False(ContentPageAccess.CanEdit(user, Me));
    }

    [Theory]
    [InlineData(Me)]
    [InlineData(null)]
    public void User_without_role_cannot_edit(string? owner)
    {
        Assert.False(ContentPageAccess.CanEdit(User(Me, "Manager"), owner));
        Assert.False(ContentPageAccess.CanEdit(User(Me), owner));
        Assert.False(ContentPageAccess.CanEdit(Anonymous(), owner));
        Assert.False(ContentPageAccess.CanEdit(null, owner));
    }

    [Fact]
    public void Draft_is_visible_only_to_those_who_can_edit()
    {
        Assert.True(ContentPageAccess.CanViewDraft(User(Me, "Creator"), Me));
        Assert.False(ContentPageAccess.CanViewDraft(User(Me, "Creator"), Other));
        Assert.True(ContentPageAccess.CanViewDraft(User("a", "Admin"), Other));
        Assert.False(ContentPageAccess.CanViewDraft(User(Me), Me));
        Assert.False(ContentPageAccess.CanViewDraft(null, Me));
    }

    [Fact]
    public void CanUseEditor_for_admin_and_creator_only()
    {
        Assert.True(ContentPageAccess.CanUseEditor(User("a", "Admin")));
        Assert.True(ContentPageAccess.CanUseEditor(User(Me, "Creator")));
        Assert.False(ContentPageAccess.CanUseEditor(User(Me, "Manager", "User")));
        Assert.False(ContentPageAccess.CanUseEditor(Anonymous()));
        Assert.False(ContentPageAccess.CanUseEditor(null));
    }

    [Fact]
    public void Media_of_public_page_is_visible_to_everyone()
    {
        PageVisibility[] v = [PageVisibility.Draft, PageVisibility.Public];
        Assert.True(ContentPageAccess.CanSeeMedia(null, v, [Other], null));
        Assert.True(ContentPageAccess.CanSeeMedia(Anonymous(), v, [Other], null));
    }

    [Fact]
    public void Media_is_visible_to_admin_even_if_unsaved()
    {
        var admin = User("a", "Admin");
        Assert.True(ContentPageAccess.CanSeeMedia(admin, None, [], Other));
        Assert.True(ContentPageAccess.CanSeeMedia(admin, [PageVisibility.Draft], [Other], null));
    }

    [Fact]
    public void Media_of_authenticated_page_needs_login()
    {
        PageVisibility[] v = [PageVisibility.Authenticated];
        Assert.True(ContentPageAccess.CanSeeMedia(User(Me), v, [Other], null));
        Assert.False(ContentPageAccess.CanSeeMedia(Anonymous(), v, [Other], null));
        Assert.False(ContentPageAccess.CanSeeMedia(null, v, [Other], null));
    }

    [Fact]
    public void Creator_sees_media_of_own_draft_page()
    {
        PageVisibility[] v = [PageVisibility.Draft];
        Assert.True(ContentPageAccess.CanSeeMedia(User(Me, "Creator"), v, [Me], null));
        Assert.False(ContentPageAccess.CanSeeMedia(User(Me, "Creator"), v, [Other], Other));
        Assert.False(ContentPageAccess.CanSeeMedia(User(Me, "Creator"), v, [null], null));
    }

    [Fact]
    public void Creator_sees_own_unsaved_upload_but_not_foreign()
    {
        var creator = User(Me, "Creator");
        Assert.True(ContentPageAccess.CanSeeMedia(creator, None, [], Me));
        Assert.False(ContentPageAccess.CanSeeMedia(creator, None, [], Other));
        Assert.False(ContentPageAccess.CanSeeMedia(creator, None, [], null));
    }

    [Fact]
    public void Draft_or_unsaved_media_is_hidden_from_anonymous_and_plain_users()
    {
        Assert.False(ContentPageAccess.CanSeeMedia(null, [PageVisibility.Draft], [Me], Me));
        Assert.False(ContentPageAccess.CanSeeMedia(Anonymous(), None, [], Me));
        Assert.False(ContentPageAccess.CanSeeMedia(User(Me), [PageVisibility.Draft], [Me], Me));
        Assert.False(ContentPageAccess.CanSeeMedia(User(Me, "Manager"), None, [], Me));
    }

    [Fact]
    public void Creator_is_a_system_role() =>
        Assert.Contains("Creator", AdminUserService.SystemRoles);
}
