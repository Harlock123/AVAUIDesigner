using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace AVAUIDesigner.Mcp;

/// <summary>
/// Saved recipes, served as MCP prompts: step-by-step plans for screens agents are often asked for (EHR charts, intake
/// forms, assessments, CRUD screens), written with the real tool names and arguments. Each step that calls a tool
/// shows the call on a line of its own, "→ tool {arguments}", so an agent can follow it as it is; the tests run every
/// recipe through the server, call by call, so a recipe that stops working fails the build.
/// </summary>
[McpServerPromptType]
public static class Recipes
{
    /// <summary>The marker that starts a step's tool call in a recipe's text.</summary>
    public const string CallMarker = "→ ";

    private const string Ns = "Clinic.Views";

    [McpServerPrompt(Name = "patient_chart", Title = "EHR patient chart")]
    [Description("A patient chart: a banner with the patient's name, age, MRN, allergies and alerts, and tabs for the summary, problems, medications, allergies, vitals and notes.")]
    public static string PatientChart(
        [Description("The view's class name (default \"PatientChart\").")] string? class_name = null)
    {
        var cls = Name(class_name, "PatientChart");
        var r = new Recipe("Build an EHR patient chart: a banner the clinician always sees, and tabs for the record's parts.");
        r.Step("Start the view (MVVM, so it binds to a view model).", "new_document", new { class_name = cls, @namespace = Ns, mvvm = true, width = 1200, height = 760, discard_changes = true });
        r.Step("Its lists show rows of types the app will define, so bind them by name at run time rather than compiling them against the view model.", "set_properties", new { target = "root", properties = new Dictionary<string, string> { ["x:CompileBindings"] = "False" } });
        r.Step("Lay out the page: the banner on top, the tabs below.", "set_grid", new { target = "layoutRoot", rows = "Auto,*" });
        r.Step("The banner: who the patient is, at a glance, with allergies in red. It binds to Patient on the view model.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <Border x:Name="patientBanner" Grid.Row="0" Background="#1E3A5F" Padding="16,10">
                  <Grid ColumnDefinitions="Auto,*,Auto">
                    <Border Grid.Column="0" Width="56" Height="56" CornerRadius="28" Background="#2E5C8A" Margin="0,0,14,0">
                      <TextBlock x:Name="initialsText" Text="{Binding Patient.Initials, FallbackValue='JS'}" Foreground="White" FontSize="20" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <StackPanel Grid.Column="1" Spacing="2" VerticalAlignment="Center">
                      <TextBlock x:Name="patientNameText" Text="{Binding Patient.DisplayName, FallbackValue='Jordan Sample'}" Foreground="White" FontSize="20" FontWeight="SemiBold" />
                      <StackPanel Orientation="Horizontal" Spacing="18">
                        <TextBlock x:Name="ageSexText" Text="{Binding Patient.AgeAndSex, FallbackValue='42 y · Female'}" Foreground="#CFE0F2" />
                        <TextBlock x:Name="dobText" Text="{Binding Patient.DateOfBirth, StringFormat='DOB {0:d}', FallbackValue='DOB 03/14/1983'}" Foreground="#CFE0F2" />
                        <TextBlock x:Name="mrnText" Text="{Binding Patient.Mrn, StringFormat='MRN {0}', FallbackValue='MRN 000-123-456'}" Foreground="#CFE0F2" />
                        <TextBlock x:Name="pcpText" Text="{Binding Patient.PrimaryCareProvider, StringFormat='PCP {0}', FallbackValue='PCP Dr Adams'}" Foreground="#CFE0F2" />
                      </StackPanel>
                    </StackPanel>
                    <StackPanel Grid.Column="2" Spacing="4" VerticalAlignment="Center" HorizontalAlignment="Right">
                      <Border Background="#C62828" CornerRadius="4" Padding="8,2">
                        <TextBlock x:Name="allergyBadge" Text="{Binding Patient.AllergySummary, StringFormat='Allergies: {0}', FallbackValue='Allergies: Penicillin'}" Foreground="White" FontWeight="SemiBold" />
                      </Border>
                      <TextBlock x:Name="codeStatusText" Text="{Binding Patient.CodeStatus, StringFormat='Code status: {0}', FallbackValue='Code status: Full code'}" Foreground="#FFD54F" HorizontalAlignment="Right" />
                    </StackPanel>
                  </Grid>
                </Border>
                """,
        });
        r.Step("The record's tabs, each a list the clinician scans: problems, medications, allergies, vitals, notes.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <TabControl x:Name="chartTabs" Grid.Row="1" Margin="8">
                  <TabItem Header="Summary">
                    <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,*" Margin="8">
                      <TextBlock Grid.Column="0" Text="Active problems" FontWeight="SemiBold" Margin="0,0,0,6" />
                      <TextBlock Grid.Column="1" Text="Current medications" FontWeight="SemiBold" Margin="12,0,0,6" />
                      <ListBox x:Name="summaryProblemsList" Grid.Row="1" Grid.Column="0" ItemsSource="{Binding ActiveProblems}" />
                      <ListBox x:Name="summaryMedicationsList" Grid.Row="1" Grid.Column="1" ItemsSource="{Binding CurrentMedications}" Margin="12,0,0,0" />
                    </Grid>
                  </TabItem>
                  <TabItem Header="Problems">
                    <DataGrid x:Name="problemsGrid" ItemsSource="{Binding Problems}" AutoGenerateColumns="False" IsReadOnly="True">
                      <DataGrid.Columns>
                        <DataGridTextColumn Header="Problem" Binding="{Binding Description}" />
                        <DataGridTextColumn Header="ICD-10" Binding="{Binding Code}" />
                        <DataGridTextColumn Header="Onset" Binding="{Binding Onset, StringFormat={}{0:d}}" />
                        <DataGridTextColumn Header="Status" Binding="{Binding Status}" />
                      </DataGrid.Columns>
                    </DataGrid>
                  </TabItem>
                  <TabItem Header="Medications">
                    <DataGrid x:Name="medicationsGrid" ItemsSource="{Binding Medications}" AutoGenerateColumns="False" IsReadOnly="True">
                      <DataGrid.Columns>
                        <DataGridTextColumn Header="Medication" Binding="{Binding Name}" />
                        <DataGridTextColumn Header="Dose" Binding="{Binding Dose}" />
                        <DataGridTextColumn Header="Route" Binding="{Binding Route}" />
                        <DataGridTextColumn Header="Frequency" Binding="{Binding Frequency}" />
                        <DataGridTextColumn Header="Prescriber" Binding="{Binding Prescriber}" />
                      </DataGrid.Columns>
                    </DataGrid>
                  </TabItem>
                  <TabItem Header="Allergies">
                    <DataGrid x:Name="allergiesGrid" ItemsSource="{Binding Allergies}" AutoGenerateColumns="False" IsReadOnly="True">
                      <DataGrid.Columns>
                        <DataGridTextColumn Header="Allergen" Binding="{Binding Allergen}" />
                        <DataGridTextColumn Header="Reaction" Binding="{Binding Reaction}" />
                        <DataGridTextColumn Header="Severity" Binding="{Binding Severity}" />
                      </DataGrid.Columns>
                    </DataGrid>
                  </TabItem>
                  <TabItem Header="Vitals">
                    <DataGrid x:Name="vitalsGrid" ItemsSource="{Binding Vitals}" AutoGenerateColumns="False" IsReadOnly="True">
                      <DataGrid.Columns>
                        <DataGridTextColumn Header="Taken" Binding="{Binding TakenAt}" />
                        <DataGridTextColumn Header="BP" Binding="{Binding BloodPressure}" />
                        <DataGridTextColumn Header="Pulse" Binding="{Binding Pulse}" />
                        <DataGridTextColumn Header="Temp" Binding="{Binding Temperature}" />
                        <DataGridTextColumn Header="SpO2" Binding="{Binding OxygenSaturation}" />
                      </DataGrid.Columns>
                    </DataGrid>
                  </TabItem>
                  <TabItem Header="Notes">
                    <DataGrid x:Name="notesGrid" ItemsSource="{Binding Notes}" AutoGenerateColumns="False" IsReadOnly="True">
                      <DataGrid.Columns>
                        <DataGridTextColumn Header="Date" Binding="{Binding Written, StringFormat={}{0:d}}" />
                        <DataGridTextColumn Header="Type" Binding="{Binding Kind}" />
                        <DataGridTextColumn Header="Author" Binding="{Binding Author}" />
                        <DataGridTextColumn Header="Summary" Binding="{Binding Summary}" />
                      </DataGrid.Columns>
                    </DataGrid>
                  </TabItem>
                </TabControl>
                """,
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("The FallbackValues are made-up sample text the designer shows (and the app shows until data arrives); never put a real patient's details in a design. Tidy anything that reads badly. The view model needs a Patient with Initials, DisplayName, AgeAndSex, DateOfBirth, Mrn, PrimaryCareProvider, AllergySummary and CodeStatus, and the lists named in the bindings: offer to save it (save_document), which writes the view model with them.");
    }

    [McpServerPrompt(Name = "intake_form", Title = "Patient intake form")]
    [Description("A new-patient intake form: demographics, contact, insurance, emergency contact, the reason for the visit and consent, in two columns, with Save and Cancel.")]
    public static string IntakeForm()
    {
        var r = new Recipe("Build a patient intake form.");
        r.Step("Start the view.", "new_document", new { class_name = "PatientIntake", @namespace = Ns, mvvm = true, width = 1000, height = 820, discard_changes = true });
        r.Step("A scrolling page with a heading.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <ScrollViewer x:Name="intakeScroll">
                  <StackPanel x:Name="intakePage" Margin="20" Spacing="10">
                    <TextBlock Text="New patient intake" FontSize="22" FontWeight="SemiBold" />
                    <TextBlock Text="Fields marked * are required." Opacity="0.7" />
                  </StackPanel>
                </ScrollViewer>
                """,
        });
        r.Step("The form, from the intake's fields: the right editor for each (dates, choices, phone, email, notes).", "add_form", new
        {
            parent = "intakePage",
            columns = 2,
            entity = new
            {
                name = "Intake",
                fields = new object[]
                {
                    new { name = "FirstName", required = true, max_length = 50 },
                    new { name = "LastName", required = true, max_length = 50 },
                    new { name = "DateOfBirth", type = "DateOnly", required = true },
                    new { name = "SexAssignedAtBirth", label = "Sex assigned at birth", options = new[] { "Female", "Male", "Intersex", "Prefer not to say" } },
                    new { name = "GenderIdentity", max_length = 40 },
                    new { name = "PreferredLanguage", options = new[] { "English", "Spanish", "Other" } },
                    new { name = "Phone", required = true, max_length = 20 },
                    new { name = "Email", max_length = 120 },
                    new { name = "Address", max_length = 120 },
                    new { name = "City", max_length = 60 },
                    new { name = "InsuranceProvider", max_length = 80 },
                    new { name = "MemberId", label = "Member ID", max_length = 30 },
                    new { name = "EmergencyContact", max_length = 80 },
                    new { name = "EmergencyPhone", max_length = 20 },
                    new { name = "ReasonForVisit", editor = "multiline", required = true },
                    new { name = "ConsentToTreat", type = "bool", label = "I consent to treatment" },
                },
            },
        });
        r.Step("The buttons.", "add_xaml", new
        {
            parent = "intakePage",
            xaml = """
                <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,8,0,0">
                  <Button x:Name="cancelButton" Content="Cancel" Command="{Binding CancelCommand}" />
                  <Button x:Name="saveButton" Content="Save intake" Classes="accent" Command="{Binding SaveCommand}" />
                </StackPanel>
                """,
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document): the view model gets the Intake property, its choices and the commands, and the Intake model class is written too.");
    }

    [McpServerPrompt(Name = "vitals_entry", Title = "Vital signs entry")]
    [Description("A vital signs entry form (blood pressure, pulse, temperature, respirations, oxygen saturation, height, weight, pain) with limits, beside a list of earlier readings.")]
    public static string VitalsEntry()
    {
        var r = new Recipe("Build a vital signs entry screen.");
        r.Step("Start the view.", "new_document", new { class_name = "VitalsEntry", @namespace = Ns, mvvm = true, width = 1100, height = 640, discard_changes = true });
        r.Step("Its lists show rows of types the app will define, so bind them by name at run time rather than compiling them against the view model.", "set_properties", new { target = "root", properties = new Dictionary<string, string> { ["x:CompileBindings"] = "False" } });
        r.Step("Two columns: the form on the left, earlier readings on the right.", "set_grid", new { target = "layoutRoot", columns = "380,*", rows = "Auto,*" });
        r.Step("Headings.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Record vitals" FontSize="18" FontWeight="SemiBold" Margin="16,12" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Earlier readings" FontSize="18" FontWeight="SemiBold" Margin="8,12" />
                <StackPanel x:Name="vitalsFormPanel" Grid.Row="1" Grid.Column="0" Margin="16,0" Spacing="8" />
                """,
        });
        r.Step("The form, with each reading's sensible limits.", "add_form", new
        {
            parent = "vitalsFormPanel",
            entity = new
            {
                name = "VitalSigns",
                fields = new object[]
                {
                    new { name = "TakenAt", label = "Taken", type = "DateTime", required = true },
                    new { name = "Systolic", type = "int", min = 50, max = 260, required = true },
                    new { name = "Diastolic", type = "int", min = 30, max = 160, required = true },
                    new { name = "Pulse", type = "int", min = 20, max = 250 },
                    new { name = "Temperature", label = "Temperature (°F)", type = "decimal", min = 90, max = 110 },
                    new { name = "Respirations", type = "int", min = 4, max = 60 },
                    new { name = "OxygenSaturation", label = "SpO₂ (%)", type = "int", min = 50, max = 100 },
                    new { name = "HeightCm", label = "Height (cm)", type = "decimal", min = 30, max = 250 },
                    new { name = "WeightKg", label = "Weight (kg)", type = "decimal", min = 0.5, max = 400 },
                    new { name = "Pain", label = "Pain (0–10)", type = "int", min = 0, max = 10 },
                },
            },
        });
        r.Step("Save, and the history.", "add_xaml", new
        {
            parent = "vitalsFormPanel",
            xaml = """<Button x:Name="recordButton" Content="Record" Classes="accent" HorizontalAlignment="Right" Command="{Binding RecordCommand}" />""",
        });
        r.Step(null, "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <DataGrid x:Name="readingsGrid" Grid.Row="1" Grid.Column="1" Margin="8,0,16,16" ItemsSource="{Binding Readings}" AutoGenerateColumns="False" IsReadOnly="True">
                  <DataGrid.Columns>
                    <DataGridTextColumn Header="Taken" Binding="{Binding TakenAt}" />
                    <DataGridTextColumn Header="BP" Binding="{Binding BloodPressure}" />
                    <DataGridTextColumn Header="Pulse" Binding="{Binding Pulse}" />
                    <DataGridTextColumn Header="Temp" Binding="{Binding Temperature}" />
                    <DataGridTextColumn Header="SpO₂" Binding="{Binding OxygenSaturation}" />
                    <DataGridTextColumn Header="Pain" Binding="{Binding Pain}" />
                  </DataGrid.Columns>
                </DataGrid>
                """,
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document).");
    }

    [McpServerPrompt(Name = "medication_list", Title = "Medication list (CRUD)")]
    [Description("A medication list screen: an editable list of a patient's medications (name, dose, route, frequency, dates, prescriber, active), with add, save and delete, and its data access code.")]
    public static string MedicationList(
        [Description("The database engine the repository is written for: sqlserver (default), postgresql, mysql, oracle, sqlite, db2.")] string? engine = null)
    {
        var r = new Recipe("Build a medication list screen with its data access.");
        r.Step("Make the CRUD screen from the Medication entity: a list beside an edit form.", "scaffold_crud", new
        {
            @namespace = Ns,
            layout = "list_detail",
            engine = string.IsNullOrWhiteSpace(engine) ? "sqlserver" : engine.Trim(),
            discard_changes = true,
            entity = new
            {
                name = "Medication",
                table = "medications",
                fields = new object[]
                {
                    new { name = "MedicationId", type = "int", is_key = true, generated = true },
                    new { name = "PatientId", type = "int", required = true, editor = "readonly" },
                    new { name = "Name", label = "Medication", required = true, max_length = 120 },
                    new { name = "Dose", required = true, max_length = 40 },
                    new { name = "Route", options = new[] { "Oral", "IV", "IM", "Subcutaneous", "Topical", "Inhaled", "Sublingual", "Rectal" } },
                    new { name = "Frequency", options = new[] { "Once daily", "Twice daily", "Three times daily", "Every 4 hours", "Every 6 hours", "At bedtime", "As needed" } },
                    new { name = "StartDate", type = "DateOnly", required = true },
                    new { name = "EndDate", type = "DateOnly" },
                    new { name = "Prescriber", max_length = 80 },
                    new { name = "Active", type = "bool" },
                    new { name = "Instructions", editor = "multiline" },
                },
            },
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document): it writes the view, its view model, the Medication model and the MedicationRepository.");
    }

    [McpServerPrompt(Name = "progress_note", Title = "SOAP progress note")]
    [Description("A progress note in SOAP form (subjective, objective, assessment, plan), with the visit's details and Sign and Save draft buttons.")]
    public static string ProgressNote()
    {
        var r = new Recipe("Build a SOAP progress note.");
        r.Step("Start the view.", "new_document", new { class_name = "ProgressNote", @namespace = Ns, mvvm = true, width = 1000, height = 860, discard_changes = true });
        r.Step("The note: who, when, then the four SOAP sections, then the buttons.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <ScrollViewer x:Name="noteScroll">
                  <StackPanel x:Name="notePage" Margin="20" Spacing="10">
                    <TextBlock Text="Progress note" FontSize="22" FontWeight="SemiBold" />
                    <Grid ColumnDefinitions="Auto,*,24,Auto,*" RowDefinitions="Auto,Auto">
                      <TextBlock Grid.Row="0" Grid.Column="0" Text="Patient" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <TextBlock x:Name="notePatientText" Grid.Row="0" Grid.Column="1" Text="{Binding PatientName}" FontWeight="SemiBold" VerticalAlignment="Center" />
                      <TextBlock Grid.Row="0" Grid.Column="3" Text="Visit date" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <CalendarDatePicker x:Name="visitDatePicker" Grid.Row="0" Grid.Column="4" SelectedDate="{Binding VisitDate, Mode=TwoWay}" />
                      <TextBlock Grid.Row="1" Grid.Column="0" Text="Visit type" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <ComboBox x:Name="visitTypeCombo" Grid.Row="1" Grid.Column="1" ItemsSource="{Binding VisitTypes}" SelectedItem="{Binding VisitType, Mode=TwoWay}" MinWidth="200" />
                      <TextBlock Grid.Row="1" Grid.Column="3" Text="Clinician" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <TextBlock x:Name="clinicianText" Grid.Row="1" Grid.Column="4" Text="{Binding Clinician}" VerticalAlignment="Center" />
                    </Grid>
                    <TextBlock Text="S: Subjective" FontWeight="SemiBold" />
                    <TextBox x:Name="subjectiveBox" Text="{Binding Subjective, Mode=TwoWay}" AcceptsReturn="True" TextWrapping="Wrap" Height="100" Watermark="The patient's account: chief complaint, history of present illness, symptoms" />
                    <TextBlock Text="O: Objective" FontWeight="SemiBold" />
                    <TextBox x:Name="objectiveBox" Text="{Binding Objective, Mode=TwoWay}" AcceptsReturn="True" TextWrapping="Wrap" Height="100" Watermark="Vitals, examination findings, results" />
                    <TextBlock Text="A: Assessment" FontWeight="SemiBold" />
                    <TextBox x:Name="assessmentBox" Text="{Binding Assessment, Mode=TwoWay}" AcceptsReturn="True" TextWrapping="Wrap" Height="90" Watermark="Diagnoses and clinical impression" />
                    <TextBlock Text="P: Plan" FontWeight="SemiBold" />
                    <TextBox x:Name="planBox" Text="{Binding Plan, Mode=TwoWay}" AcceptsReturn="True" TextWrapping="Wrap" Height="90" Watermark="Orders, medications, referrals, follow-up, patient education" />
                    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                      <Button x:Name="saveDraftButton" Content="Save draft" Command="{Binding SaveDraftCommand}" />
                      <Button x:Name="signButton" Content="Sign note" Classes="accent" Command="{Binding SignCommand}" />
                    </StackPanel>
                  </StackPanel>
                </ScrollViewer>
                """,
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document).");
    }

    [McpServerPrompt(Name = "phq9_screening", Title = "PHQ-9 depression screening")]
    [Description("A PHQ-9 depression screening form, scored as it is filled in (total and severity), with the item 9 safety alert.")]
    public static string Phq9Screening()
    {
        var r = new Recipe("Build a PHQ-9 screening form.");
        r.Step("Start the view.", "new_document", new { class_name = "Phq9Screening", @namespace = Ns, mvvm = true, width = 1000, height = 820, discard_changes = true });
        r.Step("A scrolling page.", "add_xaml", new { parent = "layoutRoot", xaml = """<ScrollViewer x:Name="screeningScroll"><StackPanel x:Name="screeningPage" Margin="20" /></ScrollViewer>""" });
        r.Step("The PHQ-9, with its scoring.", "add_assessment", new { parent = "screeningPage", instrument = "phq9" });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document): the view model gets a rating per item, the total, the severity and the alert.");
    }

    [McpServerPrompt(Name = "cans_assessment", Title = "CANS assessment")]
    [Description("A CANS (Child and Adolescent Needs and Strengths) assessment: who and when, then each domain's items rated 0 to 3, with the needs that call for action and the strengths to build on.")]
    public static string CansAssessment()
    {
        var r = new Recipe("Build a CANS assessment. The built-in item set is a core set: if the program uses its own CANS version, describe it with add_assessment's definition instead.");
        r.Step("Start the view.", "new_document", new { class_name = "CansAssessment", @namespace = Ns, mvvm = true, width = 1100, height = 900, discard_changes = true });
        r.Step("The page and the header: the child, the assessment date, the reason and the assessor.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <ScrollViewer x:Name="cansScroll">
                  <StackPanel x:Name="cansPage" Margin="20" Spacing="10">
                    <Grid ColumnDefinitions="Auto,*,24,Auto,*" RowDefinitions="Auto,Auto">
                      <TextBlock Grid.Row="0" Grid.Column="0" Text="Child" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <TextBlock x:Name="childNameText" Grid.Row="0" Grid.Column="1" Text="{Binding ChildName}" FontWeight="SemiBold" VerticalAlignment="Center" />
                      <TextBlock Grid.Row="0" Grid.Column="3" Text="Assessment date" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <CalendarDatePicker x:Name="assessmentDatePicker" Grid.Row="0" Grid.Column="4" SelectedDate="{Binding AssessmentDate, Mode=TwoWay}" />
                      <TextBlock Grid.Row="1" Grid.Column="0" Text="Reason" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <ComboBox x:Name="reasonCombo" Grid.Row="1" Grid.Column="1" ItemsSource="{Binding Reasons}" SelectedItem="{Binding Reason, Mode=TwoWay}" MinWidth="200" />
                      <TextBlock Grid.Row="1" Grid.Column="3" Text="Assessor" VerticalAlignment="Center" Margin="0,4,12,4" />
                      <TextBox x:Name="assessorBox" Grid.Row="1" Grid.Column="4" Text="{Binding Assessor, Mode=TwoWay}" />
                    </Grid>
                  </StackPanel>
                </ScrollViewer>
                """,
        });
        r.Step("The CANS items, by domain, with the result.", "add_assessment", new { parent = "cansPage", instrument = "cans" });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document).");
    }

    [McpServerPrompt(Name = "custom_assessment", Title = "Another rated assessment (Morse Fall Scale)")]
    [Description("Any rated assessment from its definition, shown with the Morse Fall Scale: each item with its own weighted choices, a total and risk bands.")]
    public static string CustomAssessment()
    {
        var r = new Recipe("Build a rated assessment from its definition: here the Morse Fall Scale, whose items each have their own weighted choices.");
        r.Step("Start the view.", "new_document", new { class_name = "FallRiskAssessment", @namespace = Ns, mvvm = true, width = 1100, height = 760, discard_changes = true });
        r.Step("A scrolling page.", "add_xaml", new { parent = "layoutRoot", xaml = """<ScrollViewer x:Name="fallScroll"><StackPanel x:Name="fallPage" Margin="20" /></ScrollViewer>""" });
        r.Step("The assessment, described: each section is one item with its own scale.", "add_assessment", new
        {
            parent = "fallPage",
            definition = new
            {
                key = "Morse",
                title = "Morse Fall Scale",
                instructions = "Score each item from the patient's history and current state.",
                scale = new[] { "0: No", "25: Yes" },
                sections = new object[]
                {
                    new { title = "History of falling", items = new[] { "Fell during this admission or in the last 3 months" } },
                    new { title = "Secondary diagnosis", items = new[] { "More than one medical diagnosis" }, scale = new[] { "0: No", "15: Yes" } },
                    new { title = "Ambulatory aid", items = new[] { "Walking aid used" }, scale = new[] { "0: None, bed rest or nurse assists", "15: Crutches, cane or walker", "30: Holds on to furniture" } },
                    new { title = "IV or saline lock", items = new[] { "Has an IV or saline lock" }, scale = new[] { "0: No", "20: Yes" } },
                    new { title = "Gait", items = new[] { "Gait" }, scale = new[] { "0: Normal, bed rest or wheelchair", "10: Weak", "20: Impaired" } },
                    new { title = "Mental status", items = new[] { "Awareness of own ability" }, scale = new[] { "0: Oriented to own ability", "15: Overestimates or forgets limits" } },
                },
                bands = new[] { "0-24: Low risk", "25-44: Moderate risk", "45-125: High risk" },
                source = "Morse Fall Scale: Janice Morse; widely used in hospitals.",
            },
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document).");
    }

    [McpServerPrompt(Name = "appointment_scheduler", Title = "Appointment scheduling")]
    [Description("An appointment scheduler: a day picker and the day's appointments on one side, and the booking form (patient, provider, type, time, length, reason) on the other.")]
    public static string AppointmentScheduler()
    {
        var r = new Recipe("Build an appointment scheduling screen.");
        r.Step("Start the view.", "new_document", new { class_name = "AppointmentScheduler", @namespace = Ns, mvvm = true, width = 1200, height = 720, discard_changes = true });
        r.Step("Its lists show rows of types the app will define, so bind them by name at run time rather than compiling them against the view model.", "set_properties", new { target = "root", properties = new Dictionary<string, string> { ["x:CompileBindings"] = "False" } });
        r.Step("Two columns.", "set_grid", new { target = "layoutRoot", columns = "*,420", rows = "Auto,*" });
        r.Step("The day and its appointments.", "add_xaml", new
        {
            parent = "layoutRoot",
            xaml = """
                <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Horizontal" Spacing="10" Margin="16,12">
                  <TextBlock Text="Schedule for" FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center" />
                  <CalendarDatePicker x:Name="dayPicker" SelectedDate="{Binding Day, Mode=TwoWay}" />
                  <ComboBox x:Name="providerFilterCombo" ItemsSource="{Binding Providers}" SelectedItem="{Binding ProviderFilter, Mode=TwoWay}" PlaceholderText="All providers" MinWidth="200" />
                </StackPanel>
                <DataGrid x:Name="appointmentsGrid" Grid.Row="1" Grid.Column="0" Margin="16,0,8,16" ItemsSource="{Binding Appointments}" SelectedItem="{Binding Appointment, Mode=TwoWay}" AutoGenerateColumns="False" IsReadOnly="True">
                  <DataGrid.Columns>
                    <DataGridTextColumn Header="Time" Binding="{Binding StartTime}" />
                    <DataGridTextColumn Header="Patient" Binding="{Binding PatientName}" />
                    <DataGridTextColumn Header="Provider" Binding="{Binding Provider}" />
                    <DataGridTextColumn Header="Type" Binding="{Binding VisitType}" />
                    <DataGridTextColumn Header="Status" Binding="{Binding Status}" />
                  </DataGrid.Columns>
                </DataGrid>
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Book an appointment" FontSize="18" FontWeight="SemiBold" Margin="8,12" />
                <StackPanel x:Name="bookingPanel" Grid.Row="1" Grid.Column="1" Margin="8,0,16,16" Spacing="8" />
                """,
        });
        r.Step("The booking form.", "add_form", new
        {
            parent = "bookingPanel",
            entity = new
            {
                name = "Appointment",
                fields = new object[]
                {
                    new { name = "PatientName", label = "Patient", required = true, max_length = 80 },
                    new { name = "Provider", required = true, options = new[] { "Dr Adams", "Dr Baker", "NP Chen" } },
                    new { name = "VisitType", label = "Visit type", options = new[] { "New patient", "Follow-up", "Annual physical", "Telehealth", "Procedure" } },
                    new { name = "Date", type = "DateOnly", required = true },
                    new { name = "StartTime", label = "Start", type = "TimeOnly", required = true },
                    new { name = "Minutes", label = "Length (minutes)", type = "int", min = 5, max = 240 },
                    new { name = "Reason", editor = "multiline" },
                },
            },
        });
        r.Step("Booking and cancelling.", "add_xaml", new
        {
            parent = "bookingPanel",
            xaml = """
                <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                  <Button x:Name="cancelAppointmentButton" Content="Cancel appointment" Command="{Binding CancelAppointmentCommand}" />
                  <Button x:Name="bookButton" Content="Book" Classes="accent" Command="{Binding BookCommand}" />
                </StackPanel>
                """,
        });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document).");
    }

    [McpServerPrompt(Name = "crud_from_table", Title = "CRUD screen from a database table")]
    [Description("A CRUD screen for a table in a connected database: read its structure (no rows), make the screen and its data access in the database's own SQL, and save it.")]
    public static string CrudFromTable(
        [Description("The connection's name (default \"clinic\").")] string? connection = null,
        [Description("The table (default \"patients\").")] string? table = null)
    {
        var conn = string.IsNullOrWhiteSpace(connection) ? "clinic" : connection.Trim();
        var t = string.IsNullOrWhiteSpace(table) ? "patients" : table.Trim();
        var r = new Recipe($"Build a CRUD screen for {t} in the {conn} database.");
        r.Step("Find the table, if its name isn't certain.", "list_tables", new { connection = conn });
        r.Step("Read its structure: columns, key, required fields and lookups.", "describe_table", new { connection = conn, table = t });
        r.Step("Make the screen and its data access.", "scaffold_crud", new { connection = conn, table = t, @namespace = Ns, layout = "list_detail", discard_changes = true });
        r.Step("Look at it.", "render_view", new { });
        r.Step("Save the view, its view model, the model and the repository.", "save_document", new { });
        return r.Text("Say which files were written and how to give the view its view model at run time (the scaffold_crud answer's wiring).");
    }

    [McpServerPrompt(Name = "crud_from_class", Title = "CRUD screen from a C# class or SQL script")]
    [Description("A CRUD screen for a class in a C# file (or a table in a SQL script) in the working folder: read it, make the screen and its data access, and look at it.")]
    public static string CrudFromClass(
        [Description("The file (default \"Models/Patient.cs\").")] string? path = null,
        [Description("The class or table (default: the one named after the file).")] string? class_name = null,
        [Description("The database engine for the repository (default sqlserver).")] string? engine = null)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "Models/Patient.cs" : path.Trim();
        var r = new Recipe($"Build a CRUD screen for the data structure in {file}.");
        r.Step("Read the entities in the file.", "read_entities", new { path = file });
        var cls = string.IsNullOrWhiteSpace(class_name) ? System.IO.Path.GetFileNameWithoutExtension(file) : class_name.Trim();
        r.Step("Make the screen for it (the class named after the file; use another if read_entities showed it).", "scaffold_crud",
            new { file, class_name = cls, @namespace = Ns, engine = string.IsNullOrWhiteSpace(engine) ? "sqlserver" : engine.Trim(), discard_changes = true });
        r.Step("Look at it.", "render_view", new { });
        return r.Text("Offer to save it (save_document). The screen uses the class as it is; the repository maps its properties to columns of the same names (or their [Column] names).");
    }

    private static string Name(string? given, string fallback) => string.IsNullOrWhiteSpace(given) ? fallback : given.Trim();

    private sealed class Recipe(string intro)
    {
        private readonly StringBuilder _text = new(intro + "\n\nFollow these steps in order. A line starting with \"" + CallMarker.Trim() + "\" is one tool call, with its arguments as JSON. If a step fails, read the error, fix the cause and carry on. Element names in these calls are the ones the steps give; if a call reports different names, use those.\n");
        private int _number;

        public void Step(string? says, string tool, object args)
        {
            _number++;
            _text.Append('\n').Append(_number).Append('.').Append(says != null ? " " : "");
            if (says != null) _text.Append(says);
            _text.Append("\n   ");
            _text.Append(CallMarker).Append(tool).Append(' ').Append(JsonSerializer.Serialize(args, Json.Options));
        }

        public string Text(string closing) => _text.Append("\n\n").Append(closing).ToString();
    }
}
