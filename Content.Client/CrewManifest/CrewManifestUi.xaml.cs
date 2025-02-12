using System.Linq;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.CrewManifest;
using Content.Shared.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CrewManifest;

[GenerateTypedNameReferences]
public sealed partial class CrewManifestUi : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;

    private readonly CrewManifestSystem _crewManifestSystem;
    private CrewManifestEntries _sourceEntries = new CrewManifestEntries();

    public CrewManifestUi()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        _crewManifestSystem = _entitySystemManager.GetEntitySystem<CrewManifestSystem>();

        StationName.AddStyleClass("LabelBig");

        TextFilter.OnTextEntered += e => Populate(StationName.Text ?? "", _sourceEntries);
        TextFilter.OnTextChanged += e => Populate(StationName.Text ?? "", _sourceEntries);
    }

    public void SetSourceEntries(CrewManifestEntries entries)
    {
        _sourceEntries = entries;
    }

    public void Populate(string name, CrewManifestEntries? entries)
    {
        CrewManifestListing.DisposeAllChildren();
        CrewManifestListing.RemoveAllChildren();

        StationNameContainer.Visible = entries != null;
        StationName.Text = name;

        if (entries == null) return;

       var entryList = SortEntries(FilterEntries(entries));

        foreach (var item in entryList)
        {
            CrewManifestListing.AddChild(new CrewManifestSection(item.section, item.entries, _resourceCache, _crewManifestSystem));
        }
    }

    private CrewManifestEntries FilterEntries(CrewManifestEntries entries)
    {
        if (string.IsNullOrWhiteSpace(TextFilter.Text))
        {
            return entries;
        }

        var result = new CrewManifestEntries();
        foreach (var entry in entries.Entries)
        {
            if (entry.Name.Contains(TextFilter.Text, StringComparison.OrdinalIgnoreCase)
                || entry.JobPrototype.Contains(TextFilter.Text, StringComparison.OrdinalIgnoreCase)
                || entry.JobTitle.Contains(TextFilter.Text, StringComparison.OrdinalIgnoreCase))
            {
                result.Entries.Add(entry);
            }
        }

        return result;
    }
    private List<(string section, List<CrewManifestEntry> entries)> SortEntries(CrewManifestEntries entries)
    {
        var entryDict = new Dictionary<string, List<CrewManifestEntry>>();

        foreach (var entry in entries.Entries)
        {
            foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
            {
                // this is a little expensive, and could be better
                if (department.Roles.Contains(entry.JobPrototype))
                {
                    entryDict.GetOrNew(department.ID).Add(entry);
                }
            }
        }

        var entryList = new List<(string section, List<CrewManifestEntry> entries)>();

        foreach (var (section, listing) in entryDict)
        {
            entryList.Add((section, listing));
        }

        var sortOrder = _configManager.GetCVar(CCVars.CrewManifestOrdering).Split(",").ToList();

        entryList.Sort((a, b) =>
        {
            var ai = sortOrder.IndexOf(a.section);
            var bi = sortOrder.IndexOf(b.section);

            // this is up here so -1 == -1 occurs first
            if (ai == bi)
                return 0;

            if (ai == -1)
                return -1;

            if (bi == -1)
                return 1;

            return ai.CompareTo(bi);
        });

        return entryList;
    }

    private sealed class CrewManifestSection : BoxContainer
    {
        public CrewManifestSection(string sectionTitle, List<CrewManifestEntry> entries, IResourceCache cache, CrewManifestSystem crewManifestSystem)
        {
            Orientation = LayoutOrientation.Vertical;
            HorizontalExpand = true;

            if (Loc.TryGetString($"department-{sectionTitle}", out var localizedDepart))
                sectionTitle = localizedDepart;

            AddChild(new Label()
            {
                StyleClasses = { "LabelBig" },
                Text = Loc.GetString(sectionTitle)
            });

            var gridContainer = new GridContainer()
            {
                HorizontalExpand = true,
                Columns = 2
            };

            AddChild(gridContainer);

            var path = new ResPath("/Textures/Interface/Misc/job_icons.rsi");
            cache.TryGetResource(path, out RSIResource? rsi);

            foreach (var entry in entries)
            {
                var name = new RichTextLabel()
                {
                    HorizontalExpand = true,
                };
                name.SetMessage(entry.Name);

                var titleContainer = new BoxContainer()
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true
                };

                var title = new RichTextLabel();
                title.SetMessage(entry.JobTitle);


                if (rsi != null)
                {
                    var icon = new TextureRect()
                    {
                        TextureScale = new Vector2(2, 2),
                        Stretch = TextureRect.StretchMode.KeepCentered
                    };

                    if (rsi.RSI.TryGetState(entry.JobIcon, out _))
                    {
                        var specifier = new SpriteSpecifier.Rsi(path, entry.JobIcon);
                        icon.Texture = specifier.Frame0();
                    }
                    else if (rsi.RSI.TryGetState("Unknown", out _))
                    {
                        var specifier = new SpriteSpecifier.Rsi(path, "Unknown");
                        icon.Texture = specifier.Frame0();
                    }

                    titleContainer.AddChild(icon);
                    titleContainer.AddChild(title);
                }
                else
                {
                    titleContainer.AddChild(title);
                }

                gridContainer.AddChild(name);
                gridContainer.AddChild(titleContainer);
            }
        }
    }
}
