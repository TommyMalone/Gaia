using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Specifications;

/// <summary>
///     Covers <see cref="SpecificationNode" />, the typed attribute accessors in their silent TryGet tier and their
///     reporting Read tier, the enumeration's among them, and the members that build and edit a tree with what each
///     refuses.
/// </summary>
/// <remarks>
///     The accessor tests read one node parsed through <see cref="XmlSpecificationReader" />, whose element carries
///     well formed and malformed attributes side by side, and each reads only the ones it is about. The building tests
///     make their own nodes, and compare what <see cref="XmlSpecificationWriter" /> writes wherever the order or the
///     spelling of what a node holds is the claim.
/// </remarks>
[TestFixture]
public class SpecificationNodeSpec
{
    [SetUp]
    public void ParseTheNode()
    {
        _logSink = new RecordingLogSink();

        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead(Snippet, SourceName, out SpecificationNode? root);

        _node = root!;
        _writer = new XmlSpecificationWriter();
    }

    private const string SourceName = "guard.xml";

    private const string Snippet = """
                                   <Region Type="Circle" Name="KillZone" Center="(0, 0, 150)" Radius="40" Ratio="1.5" Attempts="10"
                                           IgnoreHeight="true" Speed="fast" Armed="yes" Offset="(1, 2)" Fraction="1,5"
                                           Corner="(0.5, -2.25)" Facing="(0, 90, 0)" Pace="run" Gait="jog"/>
                                   """;

    private RecordingLogSink _logSink;
    private SpecificationNode _node;
    private XmlSpecificationWriter _writer;

    [Test]
    public void ReadsEachKindThroughItsReadAccessor()
    {
        Vector3 killZoneCenter = new(0f, 0f, 150f);

        Assert.Multiple(() =>
        {
            Assert.That(_node.ReadString("Name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("KillZone"));
            Assert.That(_node.ReadFloat("Radius", out float radius), Is.True);
            Assert.That(radius, Is.EqualTo(40f));
            Assert.That(_node.ReadInt("Attempts", out int attempts), Is.True);
            Assert.That(attempts, Is.EqualTo(10));
            Assert.That(_node.ReadBool("IgnoreHeight", out bool ignoreHeight), Is.True);
            Assert.That(ignoreHeight, Is.True);
            Assert.That(_node.ReadVector3("Center", out Vector3 center), Is.True);
            Assert.That(center, Is.EqualTo(killZoneCenter));
            Assert.That(_node.ReadVector2("Corner", out Vector2 corner), Is.True);
            Assert.That(corner, Is.EqualTo(new Vector2(0.5f, -2.25f)));
            Assert.That(_node.ReadRotation("Facing", out Rotation facing), Is.True);
            Assert.That(facing.Forward.X, Is.EqualTo(1f).Within(0.001f), "a yaw of 90 faces right");
            Assert.That(_node.ReadEnumeration("Pace", out Pace pace), Is.True);
            Assert.That(pace, Is.EqualTo(Pace.Run));
            Assert.That(_logSink.Entries, Is.Empty, "well formed reads report nothing");
        });
    }

    [Test]
    public void ReportsARequiredAttributeThatIsMissing()
    {
        bool wasHeightRead = _node.ReadFloat("Height", out _);
        bool wasShelteredRead = _node.ReadBool("Sheltered", out _);
        bool wasLabelRead = _node.ReadString("Label", out _);
        bool wasWaypointRead = _node.ReadVector3("Waypoint", out _);
        bool wasStrideRead = _node.ReadEnumeration<Pace>("Stride", out _);
        bool wasRoundsRead = _node.ReadInt("Rounds", out _);
        bool wasHeadingRead = _node.ReadRotation("Heading", out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasHeightRead, Is.False);
            Assert.That(wasShelteredRead, Is.False);
            Assert.That(wasLabelRead, Is.False);
            Assert.That(wasWaypointRead, Is.False);
            Assert.That(wasStrideRead, Is.False);
            Assert.That(wasRoundsRead, Is.False);
            Assert.That(wasHeadingRead, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has no Height attribute",
                "Error guard.xml Line 1, the Region element has no Sheltered attribute",
                "Error guard.xml Line 1, the Region element has no Label attribute",
                "Error guard.xml Line 1, the Region element has no Waypoint attribute",
                "Error guard.xml Line 1, the Region element has no Stride attribute",
                "Error guard.xml Line 1, the Region element has no Rounds attribute",
                "Error guard.xml Line 1, the Region element has no Heading attribute",
            }));
        });
    }

    [Test]
    public void NamesTheElementAsTheDocumentSpellsIt()
    {
        // AiStringTable keeps the casing it met first, so the name is planted in one casing before the document is
        // parsed in another. Each of the three reports then shows which spelling it prints.
        AiString planted = new("Gizmo");

        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead("""<gIZMO Armed="yes" Rank="1" Veto="true"/>""", SourceName, out SpecificationNode? root);

        root!.ReadBool("Armed", out _);
        root.ReadFloat("Height", out _);
        root.ReportConflict("Veto", "Rank");

        Assert.Multiple(() =>
        {
            Assert.That(planted.Text, Is.EqualTo("Gizmo"), "the table met the name in the planted casing first");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the gIZMO element's Armed is not true or false, it reads \"yes\"",
                "Error guard.xml Line 1, the gIZMO element has no Height attribute",
                "Error guard.xml Line 1, the gIZMO element mixes Veto with Rank",
            }));
        });
    }

    [Test]
    public void ReportsAMalformedValueThroughEitherTier()
    {
        bool hasSpeed = _node.TryGetFloat("Speed", out _);
        bool wasArmedRead = _node.ReadBool("Armed", out _);
        bool hasOffset = _node.TryGetVector3("Offset", out _);
        bool wasCenterReadFlat = _node.ReadVector2("Center", out _);
        bool hasOffsetAsRotation = _node.TryGetRotation("Offset", out _);
        bool hasFractionCount = _node.TryGetInt("Fraction", out _);
        bool hasGait = _node.TryGetEnumeration("Gait", out Pace _);

        Assert.Multiple(() =>
        {
            Assert.That(hasSpeed, Is.False);
            Assert.That(wasArmedRead, Is.False);
            Assert.That(hasOffset, Is.False);
            Assert.That(wasCenterReadFlat, Is.False, "three components is not a point in a plane");
            Assert.That(hasOffsetAsRotation, Is.False);
            Assert.That(hasFractionCount, Is.False);
            Assert.That(hasGait, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                // @formatter:off
                "Error guard.xml Line 1, the Region element's Speed is not a number, it reads \"fast\"",
                "Error guard.xml Line 1, the Region element's Armed is not true or false, it reads \"yes\"",
                "Error guard.xml Line 1, the Region element's Offset is not a tuple like (0, 1.5, 300), it reads \"(1, 2)\"",
                "Error guard.xml Line 1, the Region element's Center is not a tuple like (0, 1.5), it reads \"(0, 0, 150)\"",
                "Error guard.xml Line 1, the Region element's Offset is not pitch, yaw and roll in degrees like (0, 45, 0), it reads \"(1, 2)\"",
                "Error guard.xml Line 1, the Region element's Fraction is not a whole number, it reads \"1,5\"",
                "Error guard.xml Line 1, the Region element's Gait is not Walk, Run or Sprint, it reads \"jog\"",
                // @formatter:on
            }));
        });
    }

    [Test]
    public void RefusesATupleMissingEitherParenthesis()
    {
        // Each text lacks one parenthesis, and dropping its first and last character leaves "1, 2, 3", which reads as
        // three numbers. So a check that took either parenthesis as enough would read each as a vector.
        SpecificationNode region = new("Region");
        region.SetAttribute("NoOpening", "11, 2, 3)");
        region.SetAttribute("NoClosing", "(1, 2, 34");

        bool wasNoOpeningRead = region.ReadVector3("NoOpening", out _);
        bool wasNoClosingRead = region.ReadVector3("NoClosing", out _);

        Assert.Multiple(() =>
        {
            Assert.That(wasNoOpeningRead, Is.False, "no opening parenthesis");
            Assert.That(wasNoClosingRead, Is.False, "no closing parenthesis");
        });
    }

    [Test]
    public void StaysSilentAboutAnOptionalAttributeThatIsAbsent()
    {
        bool hasHeight = _node.TryGetFloat("Height", out _);
        bool hasVisible = _node.TryGetBool("Visible", out _);
        bool hasOrigin = _node.TryGetVector2("Origin", out _);
        bool hasLabel = _node.TryGetString("Label", out _);
        bool hasStride = _node.TryGetEnumeration<Pace>("Stride", out _);

        Assert.Multiple(() =>
        {
            Assert.That(hasHeight, Is.False);
            Assert.That(hasVisible, Is.False);
            Assert.That(hasOrigin, Is.False);
            Assert.That(hasLabel, Is.False);
            Assert.That(hasStride, Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ParsesWithInvariantCulture()
    {
        // A comma is a tuple separator rather than a decimal mark, whatever culture the host runs under.
        bool hasRatio = _node.ReadFloat("Ratio", out float ratio);
        bool hasFraction = _node.ReadFloat("Fraction", out _);

        Assert.Multiple(() =>
        {
            Assert.That(hasRatio, Is.True);
            Assert.That(ratio, Is.EqualTo(1.5f));
            Assert.That(hasFraction, Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element's Fraction is not a number, it reads \"1,5\"",
            }));
        });
    }

    [Test]
    public void KnowsWhichAttributesTheElementCarries()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_node.HasAttribute("Radius"), Is.True);
            Assert.That(_node.HasAttribute("Missing"), Is.False);
            Assert.That(_logSink.Entries, Is.Empty, "presence checks report nothing");
        });
    }

    [Test]
    public void ReadsABoolInAnyCasing()
    {
        XmlSpecificationReader reader = new(_logSink);

        reader.TryRead("""
                       <Region Marked="True" Guarded="FALSE"/>
                       """, SourceName, out SpecificationNode? root);

        Assert.Multiple(() =>
        {
            Assert.That(root!.ReadBool("Marked", out bool marked), Is.True);
            Assert.That(marked, Is.True);
            Assert.That(root.ReadBool("Guarded", out bool guarded), Is.True);
            Assert.That(guarded, Is.False);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase("walk", nameof(Pace.Walk))]
    [TestCase("RUN", nameof(Pace.Run))]
    [TestCase("sPRINT", nameof(Pace.Sprint))]
    public void ReadsAnEnumerationNameInAnyCasing(string text, string member)
    {
        SpecificationNode probe = ParseProbe($"<Probe Pace=\"{text}\"/>");

        Assert.Multiple(() =>
        {
            Assert.That(probe.ReadEnumeration("Pace", out Pace pace), Is.True);
            Assert.That(pace.ToString(), Is.EqualTo(member));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase("1")]
    [TestCase("0")]
    [TestCase("Walk, Run")]
    [TestCase("")]
    [TestCase(" Walk")]
    [TestCase("Walk ")]
    [TestCase("Jog")]
    public void RefusesTextThatIsNotOneWholeName(string text)
    {
        // Enum.TryParse, ignoring casing, reads every case but the empty text and Jog. It reads "1" as Run, "0" and
        // either spaced name as Walk, and "Walk, Run" as Run, the two values combined. A reader that trims reads the
        // two spaced names.
        SpecificationNode probe = ParseProbe($"<Probe Pace=\"{text}\"/>");

        Assert.Multiple(() =>
        {
            Assert.That(probe.ReadEnumeration("Pace", out Pace _), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                $"Error guard.xml Line 1, the Probe element's Pace is not Walk, Run or Sprint, it reads \"{text}\"",
            }));
        });
    }

    [Test]
    public void ReadsAMemberDeclaredOutOfOrderAsItsOwnValue()
    {
        // Names taken in declared order beside values taken in value order would read Second as 1.
        SpecificationNode probe = ParseProbe("<Probe Later=\"Second\" Earlier=\"First\"/>");

        Assert.Multiple(() =>
        {
            Assert.That(probe.ReadEnumeration("Later", out Order later), Is.True);
            Assert.That((int)later, Is.EqualTo(2));
            Assert.That(probe.ReadEnumeration("Earlier", out Order earlier), Is.True);
            Assert.That((int)earlier, Is.EqualTo(1));
        });
    }

    [Test]
    public void ReadsEachAliasAsTheValueItShares()
    {
        SpecificationNode probe = ParseProbe("<Probe Floor=\"Bottom\" Ground=\"low\" Top=\"High\"/>");

        Assert.Multiple(() =>
        {
            Assert.That(probe.ReadEnumeration("Floor", out Level floor), Is.True);
            Assert.That((int)floor, Is.EqualTo(0));
            Assert.That(probe.ReadEnumeration("Ground", out Level ground), Is.True);
            Assert.That((int)ground, Is.EqualTo(0));
            Assert.That(probe.ReadEnumeration("Top", out Level top), Is.True);
            Assert.That((int)top, Is.EqualTo(1));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase(TypeArgs = new[] { typeof(Mask) })]
    [TestCase(TypeArgs = new[] { typeof(Empty) })]
    [TestCase(TypeArgs = new[] { typeof(Twins) })]
    public void RefusesAnEnumerationAFileCannotNameMemberByMember<TEnum>() where TEnum : struct, Enum
    {
        // The reader that is silent about absence is asked for an attribute the element does not carry, so a check
        // made only once the text is found lets it pass.
        SpecificationNode probe = new("Probe");
        probe.SetAttribute("Choice", "Fast");
        string typeName = typeof(TEnum).Name;

        Assert.Multiple(() =>
        {
            Assert.That(() => probe.ReadEnumeration("Choice", out TEnum _),
                Throws.ArgumentException.With.Message.Contains(typeName));
            Assert.That(() => probe.TryGetEnumeration("Absent", out TEnum _),
                Throws.ArgumentException.With.Message.Contains(typeName));
            Assert.That(() => AttributeDescription.Enumeration<TEnum>("Choice", "Choice", "Fast", "A choice."),
                Throws.ArgumentException.With.Message.Contains(typeName));
        });
    }

    [Test]
    public void ReadsEveryChoiceItsDescriptionLists()
    {
        AttributeDescription described = AttributeDescription.Enumeration<Pace>("Pace", "Pace", "Walk", "How fast.");
        SpecificationNode probe = new("Probe");

        Assert.Multiple(() =>
        {
            Assert.That(described.Choices, Has.Count.EqualTo(3));

            foreach (string choice in described.Choices!)
            {
                probe.SetAttribute("Pace", choice);

                Assert.That(probe.ReadEnumeration("Pace", out Pace pace), Is.True, $"the choice {choice} reads");
                Assert.That(pace.ToString(), Is.EqualTo(choice));
            }
        });
    }

    [Test]
    public void ReadsAnEnumerationWithoutAllocating()
    {
        // The type's table is built the first time the type is read, so one read is made before the reads measured.
        const int reads = 1000;
        AiString paceName = "Pace";

        _node.ReadEnumeration(paceName, out Pace _);

        int readsThatSucceeded = 0;
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int read = 0; read < reads; read++)
        {
            if (_node.ReadEnumeration(paceName, out Pace _))
            {
                readsThatSucceeded++;
            }
        }

        long bytesAllocated = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;

        Assert.Multiple(() =>
        {
            Assert.That(readsThatSucceeded, Is.EqualTo(reads));
            Assert.That(bytesAllocated, Is.EqualTo(0L),
                $"reading {reads} enumeration attributes must not feed the collector");
        });
    }

    [Test]
    public void ReadsAnAttributeThatWasSetInCode()
    {
        SpecificationNode region = new("Region", "Circle");
        region.SetAttribute("Radius", "40");

        bool wasRead = region.ReadFloat("radius", out float radius);

        Assert.Multiple(() =>
        {
            Assert.That(wasRead, Is.True, "a name matches in any casing");
            Assert.That(radius, Is.EqualTo(40f));
        });
    }

    [Test]
    public void KeepsTheSpellingsItWasBuiltWith()
    {
        SpecificationNode region = new("rEGION", "cIRCLE");
        region.SetAttribute("rADIUS", "40");

        Assert.Multiple(() =>
        {
            Assert.That(region.NameSpelling, Is.EqualTo("rEGION"));
            Assert.That(region.TypeSpelling, Is.EqualTo("cIRCLE"));
            Assert.That(region.AttributeSpellings, Is.EqualTo(new[] { "rADIUS" }));
        });
    }

    [TestCase("A Region")]
    [TestCase("gaia:Region")]
    [TestCase("")]
    [TestCase("xmlns")]
    [TestCase("7Region")]
    public void RefusesAnElementNameTheWriterCouldNotWrite(string name)
    {
        Assert.That(() => new SpecificationNode(name), Throws.ArgumentException);
    }

    [TestCase("A Radius")]
    [TestCase("gaia:Radius")]
    [TestCase("")]
    [TestCase("xmlns")]
    public void RefusesAnAttributeNameTheWriterCouldNotWrite(string name)
    {
        SpecificationNode region = new("Region");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.SetAttribute(name, "4"), Throws.ArgumentException);
            Assert.That(() => region.InsertAttribute(0, name, "4"), Throws.ArgumentException);
            Assert.That(region.AttributeSpellings, Is.Empty);
        });
    }

    [Test]
    public void SaysThatAColonIsRefusedBecauseNoNamespaceIsRead()
    {
        Assert.That(() => new SpecificationNode("gaia:Region"),
            Throws.ArgumentException.With.Message.Contains("Gaia reads no XML namespace"));
    }

    [Test]
    public void TakesXmlnsInAnotherCasingAsAnOrdinaryName()
    {
        SpecificationNode node = new("XMLNS");
        node.SetAttribute("Xmlns", "4");

        Assert.That(_writer.Write(node), Is.EqualTo("<XMLNS Xmlns=\"4\"/>\n"));
    }

    [TestCase(0x0001)]
    [TestCase(0xFFFE)]
    [TestCase(0xD800)]
    [TestCase(0xDC00)]
    public void RefusesAValueHoldingACharacterXmlCannotCarry(int character)
    {
        string value = "a" + (char)character + "b";
        SpecificationNode region = new("Region");
        region.SetAttribute("Note", "kept");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.SetAttribute("Note", value), Throws.ArgumentException);
            Assert.That(() => region.InsertAttribute(0, "Other", value), Throws.ArgumentException);
            Assert.That(_writer.Write(region), Is.EqualTo("<Region Note=\"kept\"/>\n"));
        });
    }

    [Test]
    public void RefusesAValueHoldingTheTwoHalvesOfACharacterInTheWrongOrder()
    {
        SpecificationNode region = new("Region");

        Assert.That(() => region.SetAttribute("Note", "a\uDC00\uD801b"), Throws.ArgumentException);
    }

    [Test]
    public void TakesAValueHoldingACharacterOutsideTheFirstPlane()
    {
        SpecificationNode region = new("Region");

        region.SetAttribute("Note", "a\uD801\uDC00b");

        Assert.That(_writer.Write(region), Is.EqualTo("<Region Note=\"a\uD801\uDC00b\"/>\n"));
    }

    [Test]
    public void RefusesATypeHoldingACharacterXmlCannotCarry()
    {
        Assert.That(() => new SpecificationNode("Region", "Cir\u0001cle"), Throws.ArgumentException);
    }

    [Test]
    public void RefusesACommentHoldingACharacterXmlCannotCarry()
    {
        SpecificationNode region = new("Region");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.AddLeadingComment("a\u0001b"), Throws.ArgumentException);
            Assert.That(() => region.AddTrailingComment("a\u0001b"), Throws.ArgumentException);
        });
    }

    [TestCase("a--b", "two hyphens end a comment")]
    [TestCase("--a", "two hyphens end a comment at its start as well")]
    [TestCase("a-", "the closing marker would make the last hyphen two")]
    [TestCase("a\\rb", "a parser turns a carriage return into a line feed")]
    [TestCase("\\ra", "a parser turns a carriage return at the start into a line feed as well")]
    public void RefusesACommentThatWouldNotReadBackAsItself(string escapedComment, string reason)
    {
        string comment = escapedComment.Replace("\\r", "\r");
        SpecificationNode region = new("Region");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.AddLeadingComment(comment), Throws.ArgumentException, reason);
            Assert.That(() => region.AddTrailingComment(comment), Throws.ArgumentException, reason);
            Assert.That(region.LeadingComments, Is.Empty);
            Assert.That(region.TrailingComments, Is.Empty);
        });
    }

    [Test]
    public void ChangesOnlyTheValueWhenANameItCarriesIsSetInAnotherCasing()
    {
        SpecificationNode region = new("Region");
        region.SetAttribute("Radius", "4");
        region.SetAttribute("Height", "2");

        region.SetAttribute("radius", "5");

        Assert.That(_writer.Write(region), Is.EqualTo("<Region Radius=\"5\" Height=\"2\"/>\n"));
    }

    [Test]
    public void RefusesToInsertANameItAlreadyCarries()
    {
        SpecificationNode region = new("Region");
        region.SetAttribute("Radius", "4");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.InsertAttribute(0, "radius", "5"), Throws.ArgumentException);
            Assert.That(_writer.Write(region), Is.EqualTo("<Region Radius=\"4\"/>\n"));
        });
    }

    [TestCase("Type")]
    [TestCase("type")]
    [TestCase("TYPE")]
    public void RefusesTheTypeAsAnAttributeInAnyCasing(string name)
    {
        SpecificationNode region = new("Region", "Circle");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.SetAttribute(name, "Sphere"), Throws.ArgumentException);
            Assert.That(() => region.InsertAttribute(0, name, "Sphere"), Throws.ArgumentException);
            Assert.That(_writer.Write(region), Is.EqualTo("<Region Type=\"Circle\"/>\n"));
        });
    }

    [Test]
    public void GivesAChildItsParent()
    {
        SpecificationNode option = new("Option");
        SpecificationNode action = new("Action");

        option.AddChild(action);

        Assert.Multiple(() =>
        {
            Assert.That(action.Parent, Is.SameAs(option));
            Assert.That(option.Parent, Is.Null);
        });
    }

    [Test]
    public void LeavesARemovedChildWithoutAParent()
    {
        SpecificationNode option = new("Option");
        SpecificationNode action = new("Action");
        option.AddChild(action);

        option.RemoveChild(action);

        Assert.Multiple(() =>
        {
            Assert.That(action.Parent, Is.Null);
            Assert.That(option.Children, Is.Empty);
        });
    }

    [Test]
    public void ReportsNoIndexForANodeThatIsNotItsChild()
    {
        SpecificationNode option = new("Option");
        SpecificationNode stranger = new("Action");

        Assert.That(option.RemoveChild(stranger), Is.EqualTo(-1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RefusesToHoldItself(bool throughInsert)
    {
        SpecificationNode option = new("Option");

        Assert.Multiple(() =>
        {
            Assert.That(() => Attach(option, option, throughInsert), Throws.ArgumentException);
            Assert.That(option.Children, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RefusesToHoldOneOfItsAncestors(bool throughInsert)
    {
        SpecificationNode brain = new("Brain");
        SpecificationNode reasoner = new("Reasoner");
        SpecificationNode option = new("Option");
        brain.AddChild(reasoner);
        reasoner.AddChild(option);

        Assert.Multiple(() =>
        {
            Assert.That(() => Attach(option, brain, throughInsert), Throws.ArgumentException);
            Assert.That(option.Children, Is.Empty);
            Assert.That(brain.Parent, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RefusesAChildThatAnotherNodeHolds(bool throughInsert)
    {
        SpecificationNode firstOption = new("Option");
        SpecificationNode secondOption = new("Option");
        SpecificationNode action = new("Action");
        firstOption.AddChild(action);

        Assert.Multiple(() =>
        {
            Assert.That(() => Attach(secondOption, action, throughInsert), Throws.ArgumentException);
            Assert.That(secondOption.Children, Is.Empty);
            Assert.That(action.Parent, Is.SameAs(firstOption));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RefusesAChildItAlreadyHolds(bool throughInsert)
    {
        SpecificationNode option = new("Option");
        SpecificationNode action = new("Action");
        option.AddChild(action);

        Assert.Multiple(() =>
        {
            Assert.That(() => Attach(option, action, throughInsert), Throws.ArgumentException);
            Assert.That(option.Children, Has.Count.EqualTo(1), "written twice, the node would read back as two");
        });
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void RefusesAChildIndexOutsideItsChildren(int index)
    {
        SpecificationNode option = new("Option");
        SpecificationNode onlyChild = new("Action");
        SpecificationNode refused = new("Consideration");
        option.AddChild(onlyChild);

        Assert.Multiple(() =>
        {
            Assert.That(() => option.InsertChild(index, refused),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("from 0 to the 1 children"));
            Assert.That(option.Children, Is.EqualTo(new[] { onlyChild }));
            Assert.That(refused.Parent, Is.Null);
        });
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void RefusesAnAttributeIndexOutsideItsAttributes(int index)
    {
        SpecificationNode region = new("Region");
        region.SetAttribute("Radius", "4");

        Assert.Multiple(() =>
        {
            Assert.That(() => region.InsertAttribute(index, "Height", "2"),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("from 0 to the 1 attributes"));
            Assert.That(_writer.Write(region), Is.EqualTo("<Region Radius=\"4\"/>\n"));
            Assert.That(region.HasAttribute("Height"), Is.False);
        });
    }

    [Test]
    public void FindsAnAttributeItCarriesInAnyCasing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_node.IndexOfAttribute("CENTER"), Is.EqualTo(1));
            Assert.That(_node.IndexOfAttribute("Missing"), Is.EqualTo(-1));
        });
    }

    [Test]
    public void ReportsThatAnAbsentAttributeWasNotRemoved()
    {
        SpecificationNode region = new("Region");
        region.SetAttribute("Radius", "4");

        Assert.Multiple(() =>
        {
            Assert.That(region.RemoveAttribute("Height"), Is.False);
            Assert.That(region.RemoveAttribute("RADIUS"), Is.True);
            Assert.That(region.HasAttribute("Radius"), Is.False);
        });
    }

    /// <summary>Parses one element through a reader of its own on the fixture's sink, under the fixture's source name.</summary>
    private SpecificationNode ParseProbe(string snippet)
    {
        XmlSpecificationReader reader = new(_logSink);
        reader.TryRead(snippet, SourceName, out SpecificationNode? probe);

        return probe!;
    }

    /// <summary>Adds a child through AddChild, or through InsertChild at the front, which refuse the same children.</summary>
    private static void Attach(SpecificationNode parent, SpecificationNode child, bool throughInsert)
    {
        if (throughInsert)
        {
            parent.InsertChild(0, child);
        }
        else
        {
            parent.AddChild(child);
        }
    }

    /// <summary>Three members valued in declared order, as the library's own enumerations are.</summary>
    private enum Pace
    {
        Walk,
        Run,
        Sprint,
    }

    /// <summary>Declared out of order, so a name read by declared position would take the wrong value.</summary>
    private enum Order
    {
        Second = 2,
        First = 1,
    }

    /// <summary>Two names for one value, each of which reads as that value.</summary>
    private enum Level
    {
        Low = 0,
        Bottom = 0,
        High = 1,
    }

    /// <summary>Values meant to be combined, which one whole name cannot express.</summary>
    [Flags]
    private enum Mask
    {
        Near = 1,
        Far = 2,
    }

    /// <summary>No members, so there is no name for a file to give.</summary>
    private enum Empty
    {
    }

    /// <summary>Two names that differ only in casing, which a reader taking a name in any casing cannot tell apart.</summary>
    private enum Twins
    {
        Fast,
        FAST,
    }
}
