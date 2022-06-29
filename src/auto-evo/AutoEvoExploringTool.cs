﻿using System.Collections.Generic;
using System.Linq;
using AutoEvo;
using Godot;
using Godot.Collections;

public class AutoEvoExploringTool : NodeWithInput
{
    // World paths

    [Export]
    public NodePath MicrobeCameraPath = null!;

    [Export]
    public NodePath GridPath = null!;

    [Export]
    public NodePath DynamicallySpawnedPath = null!;

    // Auto-evo config paths

    [Export]
    public NodePath AllowSpeciesToNotMutatePath = null!;

    [Export]
    public NodePath AllowSpeciesToNotMigratePath = null!;

    [Export]
    public NodePath BiodiversityAttemptFillChancePath = null!;

    [Export]
    public NodePath BiodiversityFromNeighbourPatchChancePath = null!;

    [Export]
    public NodePath BiodiversityNearbyPatchIsFreePopulationPath = null!;

    [Export]
    public NodePath BiodiversitySplitIsMutatedPath = null!;

    [Export]
    public NodePath LowBiodiversityLimitPath = null!;

    [Export]
    public NodePath MaximumSpeciesInPatchPath = null!;

    [Export]
    public NodePath MoveAttemptsPerSpeciesPath = null!;

    [Export]
    public NodePath MutationsPerSpeciesPath = null!;

    [Export]
    public NodePath NewBiodiversityIncreasingSpeciesPopulationPath = null!;

    [Export]
    public NodePath ProtectMigrationsFromSpeciesCapPath = null!;

    [Export]
    public NodePath ProtectNewCellsFromSpeciesCapPath = null!;

    [Export]
    public NodePath RefundMigrationsInExtinctionsPath = null!;

    [Export]
    public NodePath StrictNicheCompetitionPath = null!;

    [Export]
    public NodePath SpeciesSplitByMutationThresholdPopulationAmountPath = null!;

    [Export]
    public NodePath SpeciesSplitByMutationThresholdPopulationFractionPath = null!;

    [Export]
    public NodePath UseBiodiversityForceSplitPath = null!;

    // Status paths

    [Export]
    public NodePath CurrentGenerationLabelPath = null!;

    [Export]
    public NodePath RunStatusLabelPath = null!;

    [Export]
    public NodePath RunGenerationButtonPath = null!;

    [Export]
    public NodePath RunStepButtonPath = null!;

    [Export]
    public NodePath AbortButtonPath = null!;

    // Report paths

    [Export]
    public NodePath HistoryContainerPath = null!;

    [Export]
    public NodePath ResultsLabelPath = null!;

    // Viewer paths

    [Export]
    public NodePath SpeciesListPath = null!;

    [Export]
    public NodePath SpeciesHistoryPath = null!;

    // Tab paths

    [Export]
    public NodePath ConfigEditorPath = null!;

    [Export]
    public NodePath ReportPath = null!;

    [Export]
    public NodePath ViewerPath = null!;

    private MicrobeCamera microbeCamera = null!;
    private Spatial dynamicallySpawned = null!;
    private Spatial grid = null!;

    // Auto-evo config related controls.
    private CustomCheckBox allowSpeciesToNotMutateCheckBox = null!;
    private CustomCheckBox allowSpeciesToNotMigrateCheckBox = null!;
    private SpinBox biodiversityAttemptFillChanceSpinBox = null!;
    private SpinBox biodiversityFromNeighbourPatchChanceSpinBox = null!;
    private CustomCheckBox biodiversityNearbyPatchIsFreePopulationCheckBox = null!;
    private CustomCheckBox biodiversitySplitIsMutatedCheckBox = null!;
    private SpinBox lowBiodiversityLimitSpinBox = null!;
    private SpinBox maximumSpeciesInPatchSpinBox = null!;
    private SpinBox moveAttemptsPerSpeciesSpinBox = null!;
    private SpinBox mutationsPerSpeciesSpinBox = null!;
    private SpinBox newBiodiversityIncreasingSpeciesPopulationSpinBox = null!;
    private CustomCheckBox protectMigrationsFromSpeciesCapCheckBox = null!;
    private CustomCheckBox protectNewCellsFromSpeciesCapCheckBox = null!;
    private CustomCheckBox refundMigrationsInExtinctionsCheckBox = null!;
    private CustomCheckBox strictNicheCompetitionCheckBox = null!;
    private SpinBox speciesSplitByMutationThresholdPopulationAmountSpinBox = null!;
    private SpinBox speciesSplitByMutationThresholdPopulationFractionSpinBox = null!;
    private CustomCheckBox useBiodiversityForceSplitCheckBox = null!;

    // Auto-evo status related controls
    private Label currentGenerationLabel = null!;
    private Label runStatusLabel = null!;
    private Button runGenerationButton = null!;
    private Button runStepButton = null!;
    private Button abortButton = null!;

    // Auto-evo report related controls
    private VBoxContainer historyContainer = null!;
    private CustomRichTextLabel resultsLabel = null!;

    // Viewer related
    private VBoxContainer speciesListContainer = null!;
    private VBoxContainer speciesHistoryContainer = null!;

    // Tabs
    private Control configEditorTab = null!;
    private Control reportTab = null!;
    private Control viewerTab = null!;

    private GameProperties gameProperties = null!;
    private AutoEvoConfiguration autoEvoConfiguration = null!;
    private AutoEvoRun? autoEvoRun;
    private int currentGeneration = 0;
    private readonly List<LocalizedStringBuilder> runResultsList = new();
    private int currentDisplayed = -1;
    private PackedScene customCheckBoxScene = null!;
    private PackedScene microbeScene = null!;
    private readonly ButtonGroup historyCheckBoxGroup = new();
    private readonly ButtonGroup speciesListCheckBoxGroup = new();
    private readonly ButtonGroup speciesHistoryCheckBoxGroup = new();
    private Microbe? displayedMicrobe;
    private bool ready;
    private readonly List<Species> speciesAlive = new();
    private readonly List<System.Collections.Generic.Dictionary<uint, Species>> speciesHistory = new();
    private int currentDisplayedGeneration = -1;

    [Signal]
    public delegate void OnAutoEvoExploringToolClosed();

    private enum TabIndex
    {
        Config,
        Report,
        Viewer,
    }

    public override void _Ready()
    {
        base._Ready();

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeIn, 0.1f, null, false);

        microbeCamera = GetNode<MicrobeCamera>(MicrobeCameraPath);
        grid = GetNode<Spatial>(GridPath);
        dynamicallySpawned = GetNode<Spatial>(DynamicallySpawnedPath);

        allowSpeciesToNotMutateCheckBox = GetNode<CustomCheckBox>(AllowSpeciesToNotMutatePath);
        allowSpeciesToNotMigrateCheckBox = GetNode<CustomCheckBox>(AllowSpeciesToNotMigratePath);
        biodiversityAttemptFillChanceSpinBox = GetNode<SpinBox>(BiodiversityAttemptFillChancePath);
        biodiversityFromNeighbourPatchChanceSpinBox = GetNode<SpinBox>(BiodiversityFromNeighbourPatchChancePath);
        biodiversitySplitIsMutatedCheckBox = GetNode<CustomCheckBox>(BiodiversitySplitIsMutatedPath);
        biodiversityNearbyPatchIsFreePopulationCheckBox =
            GetNode<CustomCheckBox>(BiodiversityNearbyPatchIsFreePopulationPath);
        lowBiodiversityLimitSpinBox = GetNode<SpinBox>(LowBiodiversityLimitPath);
        maximumSpeciesInPatchSpinBox = GetNode<SpinBox>(MaximumSpeciesInPatchPath);
        moveAttemptsPerSpeciesSpinBox = GetNode<SpinBox>(MoveAttemptsPerSpeciesPath);
        mutationsPerSpeciesSpinBox = GetNode<SpinBox>(MutationsPerSpeciesPath);
        newBiodiversityIncreasingSpeciesPopulationSpinBox =
            GetNode<SpinBox>(NewBiodiversityIncreasingSpeciesPopulationPath);
        protectMigrationsFromSpeciesCapCheckBox = GetNode<CustomCheckBox>(ProtectMigrationsFromSpeciesCapPath);
        protectNewCellsFromSpeciesCapCheckBox = GetNode<CustomCheckBox>(ProtectNewCellsFromSpeciesCapPath);
        refundMigrationsInExtinctionsCheckBox = GetNode<CustomCheckBox>(RefundMigrationsInExtinctionsPath);
        strictNicheCompetitionCheckBox = GetNode<CustomCheckBox>(StrictNicheCompetitionPath);
        speciesSplitByMutationThresholdPopulationAmountSpinBox =
            GetNode<SpinBox>(SpeciesSplitByMutationThresholdPopulationAmountPath);
        speciesSplitByMutationThresholdPopulationFractionSpinBox =
            GetNode<SpinBox>(SpeciesSplitByMutationThresholdPopulationFractionPath);
        useBiodiversityForceSplitCheckBox = GetNode<CustomCheckBox>(UseBiodiversityForceSplitPath);

        currentGenerationLabel = GetNode<Label>(CurrentGenerationLabelPath);
        runStatusLabel = GetNode<Label>(RunStatusLabelPath);
        runGenerationButton = GetNode<Button>(RunGenerationButtonPath);
        runStepButton = GetNode<Button>(RunStepButtonPath);
        abortButton = GetNode<Button>(AbortButtonPath);

        resultsLabel = GetNode<CustomRichTextLabel>(ResultsLabelPath);
        historyContainer = GetNode<VBoxContainer>(HistoryContainerPath);

        speciesListContainer = GetNode<VBoxContainer>(SpeciesListPath);
        speciesHistoryContainer = GetNode<VBoxContainer>(SpeciesHistoryPath);

        configEditorTab = GetNode<Control>(ConfigEditorPath);
        reportTab = GetNode<Control>(ReportPath);
        viewerTab = GetNode<Control>(ViewerPath);

        customCheckBoxScene = GD.Load<PackedScene>("res://src/gui_common/CustomCheckBox.tscn");
        microbeScene = GD.Load<PackedScene>("res://src/microbe_stage/Microbe.tscn");

        Init();

        ready = true;
    }

    public override void _Process(float delta)
    {
        base._Process(delta);

        if (autoEvoRun != null)
        {
            runStatusLabel.Text = autoEvoRun.Status;

            if (autoEvoRun.WasSuccessful)
            {
                ApplyAutoEvoRun();

                // Clear autoEvoRun and enable buttons to allow the next run to start.
                autoEvoRun = null;
                runGenerationButton.Disabled = false;
                runStepButton.Disabled = false;
                abortButton.Disabled = true;
            }
            else if (autoEvoRun.Aborted)
            {
                // Clear autoEvoRun and enable buttons to allow the next run to start.
                autoEvoRun = null;
                runGenerationButton.Disabled = false;
                runStepButton.Disabled = false;
                abortButton.Disabled = true;
            }
        }

        grid.Translation = microbeCamera.CursorWorldPos;
    }

    [RunOnKeyDown("ui_cancel")]
    public bool OnBackButtonPressed()
    {
        // TODO: Ask to return

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeOut, 0.1f,
            SceneManager.Instance.ReturnToMenu, false);

        return true;
    }

    /// <summary>
    ///   Initialize the exploring tool
    /// </summary>
    private void Init()
    {
        currentGeneration = 0;
        currentGenerationLabel.Text = currentGeneration.ToString();
        runStatusLabel.Text = TranslationServer.Translate("READY");
        gameProperties = GameProperties.StartNewMicrobeGame(new WorldGenerationSettings());
        autoEvoConfiguration = (AutoEvoConfiguration)SimulationParameters.Instance.AutoEvoConfiguration.Clone();

        // Init all config controls
        allowSpeciesToNotMutateCheckBox.Pressed = autoEvoConfiguration.AllowSpeciesToNotMutate;
        allowSpeciesToNotMigrateCheckBox.Pressed = autoEvoConfiguration.AllowSpeciesToNotMigrate;
        biodiversityAttemptFillChanceSpinBox.Value = autoEvoConfiguration.BiodiversityAttemptFillChance;
        biodiversityFromNeighbourPatchChanceSpinBox.Value = autoEvoConfiguration.BiodiversityFromNeighbourPatchChance;
        biodiversitySplitIsMutatedCheckBox.Pressed = autoEvoConfiguration.BiodiversityNearbyPatchIsFreePopulation;
        biodiversityNearbyPatchIsFreePopulationCheckBox.Pressed = autoEvoConfiguration.BiodiversitySplitIsMutated;
        lowBiodiversityLimitSpinBox.Value = autoEvoConfiguration.LowBiodiversityLimit;
        maximumSpeciesInPatchSpinBox.Value = autoEvoConfiguration.MaximumSpeciesInPatch;
        moveAttemptsPerSpeciesSpinBox.Value = autoEvoConfiguration.MoveAttemptsPerSpecies;
        mutationsPerSpeciesSpinBox.Value = autoEvoConfiguration.MutationsPerSpecies;
        newBiodiversityIncreasingSpeciesPopulationSpinBox.Value =
            autoEvoConfiguration.NewBiodiversityIncreasingSpeciesPopulation;
        protectMigrationsFromSpeciesCapCheckBox.Pressed = autoEvoConfiguration.ProtectMigrationsFromSpeciesCap;
        protectNewCellsFromSpeciesCapCheckBox.Pressed = autoEvoConfiguration.ProtectNewCellsFromSpeciesCap;
        refundMigrationsInExtinctionsCheckBox.Pressed = autoEvoConfiguration.RefundMigrationsInExtinctions;
        strictNicheCompetitionCheckBox.Pressed = autoEvoConfiguration.StrictNicheCompetition;
        speciesSplitByMutationThresholdPopulationAmountSpinBox.Value =
            autoEvoConfiguration.SpeciesSplitByMutationThresholdPopulationAmount;
        speciesSplitByMutationThresholdPopulationFractionSpinBox.Value =
            autoEvoConfiguration.SpeciesSplitByMutationThresholdPopulationFraction;
        useBiodiversityForceSplitCheckBox.Pressed = autoEvoConfiguration.UseBiodiversityForceSplit;
    }

    /*
    /// <summary>
    ///   Clean the exploring tool for next entrance
    /// </summary>
    private void Clean()
    {
        autoEvoConfiguration = null!;
        gameProperties = null!;
        resultsLabel.ExtendedBbcode = string.Empty;
        runResultsList.Clear();

        foreach (var checkBox in historyCheckBoxes)
            checkBox.DetachAndQueueFree();

        historyCheckBoxes.Clear();
    }
    */

    /// <summary>
    ///   This function updates all configurations in a row to avoid adding numerous separate callback functions.
    /// </summary>
    /// <param name="value">
    ///   Godot Signal parameter, 'state' from Button::toggled or 'value' from SpinBox::value_changed.
    /// </param>
    private void UpdateAutoEvoConfiguration(object? value = null)
    {
        _ = value;

        if (!ready)
            return;

        autoEvoConfiguration.AllowSpeciesToNotMutate = allowSpeciesToNotMutateCheckBox.Pressed;
        autoEvoConfiguration.AllowSpeciesToNotMigrate = allowSpeciesToNotMigrateCheckBox.Pressed;
        autoEvoConfiguration.BiodiversityAttemptFillChance = (int)biodiversityAttemptFillChanceSpinBox.Value;
        autoEvoConfiguration.BiodiversityFromNeighbourPatchChance =
            (float)biodiversityFromNeighbourPatchChanceSpinBox.Value;
        autoEvoConfiguration.BiodiversityNearbyPatchIsFreePopulation = biodiversitySplitIsMutatedCheckBox.Pressed;
        autoEvoConfiguration.BiodiversitySplitIsMutated = biodiversityNearbyPatchIsFreePopulationCheckBox.Pressed;
        autoEvoConfiguration.LowBiodiversityLimit = (int)lowBiodiversityLimitSpinBox.Value;
        autoEvoConfiguration.MaximumSpeciesInPatch = (int)maximumSpeciesInPatchSpinBox.Value;
        autoEvoConfiguration.MoveAttemptsPerSpecies = (int)moveAttemptsPerSpeciesSpinBox.Value;
        autoEvoConfiguration.MutationsPerSpecies = (int)mutationsPerSpeciesSpinBox.Value;
        autoEvoConfiguration.NewBiodiversityIncreasingSpeciesPopulation =
            (int)newBiodiversityIncreasingSpeciesPopulationSpinBox.Value;
        autoEvoConfiguration.ProtectMigrationsFromSpeciesCap = protectMigrationsFromSpeciesCapCheckBox.Pressed;
        autoEvoConfiguration.ProtectNewCellsFromSpeciesCap = protectNewCellsFromSpeciesCapCheckBox.Pressed;
        autoEvoConfiguration.RefundMigrationsInExtinctions = refundMigrationsInExtinctionsCheckBox.Pressed;
        autoEvoConfiguration.StrictNicheCompetition = strictNicheCompetitionCheckBox.Pressed;
        autoEvoConfiguration.SpeciesSplitByMutationThresholdPopulationAmount =
            (int)speciesSplitByMutationThresholdPopulationAmountSpinBox.Value;
        autoEvoConfiguration.SpeciesSplitByMutationThresholdPopulationFraction =
            (float)speciesSplitByMutationThresholdPopulationFractionSpinBox.Value;
        autoEvoConfiguration.UseBiodiversityForceSplit = useBiodiversityForceSplitCheckBox.Pressed;
    }

    /// <summary>
    ///   Run a new generation or finish the current generation
    /// </summary>
    private void OnRunGenerationButtonPressed()
    {
        // If the previous one has finished / failed
        if (autoEvoRun?.Aborted != false || autoEvoRun.Finished)
        {
            autoEvoRun = new AutoEvoRun(gameProperties.GameWorld, autoEvoConfiguration) { FullSpeed = true };
            autoEvoRun.Start();
        }
        else
        {
            autoEvoRun.FullSpeed = true;
            autoEvoRun.Continue();
        }

        // Disable these buttons
        runGenerationButton.Disabled = true;
        runStepButton.Disabled = true;
        abortButton.Disabled = false;
    }

    private void OnRunStepButtonPressed()
    {
        if (autoEvoRun?.Aborted != false || autoEvoRun.Finished)
        {
            autoEvoRun = new AutoEvoRun(gameProperties.GameWorld, autoEvoConfiguration);
        }

        // To avoid concurrent steps
        autoEvoRun.FullSpeed = false;
        autoEvoRun.OneStep();
        abortButton.Disabled = false;
    }

    private void OnAbortButtonPressed()
    {
        if (autoEvoRun?.WasSuccessful == false)
            autoEvoRun.Abort();

        runGenerationButton.Disabled = false;
        runStepButton.Disabled = false;
    }

    private void UpdateResults()
    {
        if (currentDisplayed < runResultsList.Count)
            resultsLabel.ExtendedBbcode = runResultsList[currentDisplayed].ToString();
    }

    private void ChangeTab(int index)
    {
        switch ((TabIndex)index)
        {
            case TabIndex.Config:
            {
                reportTab.Visible = false;
                viewerTab.Visible = false;
                configEditorTab.Visible = true;
                break;
            }

            case TabIndex.Report:
            {
                configEditorTab.Visible = false;
                viewerTab.Visible = false;
                reportTab.Visible = true;
                break;
            }

            case TabIndex.Viewer:
            {
                reportTab.Visible = false;
                configEditorTab.Visible = false;
                viewerTab.Visible = true;
                break;
            }
        }
    }

    private void HistoryCheckBoxToggled(bool state, int index)
    {
        if (state)
        {
            currentDisplayed = index;
            UpdateResults();
        }
    }

    private void ApplyAutoEvoRun()
    {
        // Add run results
        RunResults results = autoEvoRun!.Results!;
        runResultsList.Add(results.MakeSummary(gameProperties.GameWorld.Map, true));

        // Add check box to history container
        using (var checkBox = customCheckBoxScene.Instance<CustomCheckBox>())
        {
            checkBox.Text = (currentGeneration + 1).ToString();
            checkBox.Connect("toggled", this, nameof(HistoryCheckBoxToggled),
                new Godot.Collections.Array { currentGeneration });
            checkBox.Group = historyCheckBoxGroup;
            historyContainer.AddChild(checkBox);

            // History checkboxes are in one button group, so this automatically releases other buttons
            // History label is updated in button toggled signal callback
            checkBox.Pressed = true;
        }

        speciesHistory.Add(gameProperties.GameWorld.Species.ToDictionary(pair => pair.Key, pair => (Species)pair.Value.Clone()));

        // Add species checkbox
        using (var checkBox = customCheckBoxScene.Instance<CustomCheckBox>())
        {
            checkBox.Text = (currentGeneration + 1).ToString();
            checkBox.Connect("toggled", this, nameof(SpeciesHistoryCheckBoxToggled),
                new Array { currentGeneration });
            checkBox.Group = speciesHistoryCheckBoxGroup;
            speciesHistoryContainer.AddChild(checkBox);

            checkBox.Pressed = true;
        }

        // Apply the results
        autoEvoRun.ApplyAllEffects(true);
        currentGenerationLabel.Text = (++currentGeneration).ToString();
    }

    private void SpeciesHistoryCheckBoxToggled(bool state, int index)
    {
        if (state && index != currentDisplayedGeneration)
        {
            foreach (Node node in speciesListContainer.GetChildren())
            {
                node.DetachAndQueueFree();
            }

            currentDisplayedGeneration = index;

            foreach (var pair in speciesHistory[currentDisplayedGeneration])
            {
                var checkBox = customCheckBoxScene.Instance<CustomCheckBox>();
                checkBox.Text = $"{pair.Key.ToString()}: {pair.Value.FormattedName}";
                checkBox.Group = speciesListCheckBoxGroup;
                checkBox.Connect("toggled", this, nameof(SpeciesListCheckBoxToggled), new Array { pair.Key });
                speciesListContainer.AddChild(checkBox);
            }
        }
    }

    private void SpeciesListCheckBoxToggled(bool state, uint species)
    {
        if (state)
        {
            displayedMicrobe?.DetachAndQueueFree();
            displayedMicrobe = microbeScene.Instance<Microbe>();
            displayedMicrobe.IsForPreviewOnly = true;
            dynamicallySpawned.AddChild(displayedMicrobe);
            displayedMicrobe.ApplySpecies(speciesHistory[currentDisplayedGeneration][species]);
        }
    }
}
