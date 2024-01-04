using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;

/*
 * Fitts Law Task for Unity Script
 */

namespace FittsTask {
    public enum CursorCenter {
        Center, TopLeft
    }
    public enum TaskType {
        OneDimensional, TwoDimensional
    }
    public enum SelectionMethod {
        MouseButton, DwellTime
    }

    public class FittsTask : MonoBehaviour {

        [Header("General Settings")]
        public Canvas screenCanvas;
        public GameObject backgroundPanel;
        public Sprite targetDiscSprite;
        public Sprite targetRectSprite;
        public Sprite cursorSprite;
        public int cursorSize = 15;
        public CursorCenter cursorCenter = CursorCenter.TopLeft;

        [Header("Experimental Settings")]
        public int SubjectID;
        public string Condition = "";
        public string Task = "";
        public string Group = "";

        [Header("Fitts Task Settings")]
        public TaskType fittsTaskType;
        public SelectionMethod taskSelectionMethod;

        public int dwellTime = 0;
        [SerializeField] private int numberOfTrials = 15;

        // Correct the value to ensure it's odd and within the range between 3 and 53 when accessed
        public int NumberOfTrials {
            get {
                numberOfTrials = Mathf.Max(3, Mathf.Min(53, numberOfTrials | 1));
                return numberOfTrials;
            }
            set {
                numberOfTrials = Mathf.Max(3, Mathf.Min(53, value | 1));
            }
        }
        void OnValidate() {
            NumberOfTrials = numberOfTrials; // Correct the value immediately when it's changed in the inspector
        }

        public int numberOfRepetitions = 1;

        public int[] amplitudes = new int[] { 100, 300, 500 };
        public int[] widths = new int[] { 20, 40, 80 };

        public bool randomizeTargetConditions = true;
        public bool renderCursorOnCanvas = true;
        public bool showAmplitudeTrials = true;
        public bool audioFeedback = true; 
        public bool mouseOverHighlight = true;

        [Header("Colors")]
        public Color backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);
        public Color foregroundColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
        public Color targetColor = Color.red;
        public Color buttonDownColor = Color.blue;
        public Color mouseOverColor = Color.yellow;
        public Color cursorColor = Color.white;

        [Header("Logging")]
        public bool saveEvents = true;
        public string eventsLogPath = @"Results/FittsLogging/Events";
        public bool saveMovements = false;
        public string movementLogPath = @"Results/FittsLogging/Movements";
        public bool saveEvaluation = true;
        public string evalationLogPath = @"Results/FittsLogging/Evaluation";

        private StreamWriter eventLogFileWriter;
        private StreamWriter movementLogFileWriter;
        private StreamWriter evaluationLogFileWriter;
        private StreamWriter summaryLogFileWriter;


        private List<GameObject> targetList = new List<GameObject>(); // To keep track of trials
        private GameObject cursor;
        private DateTime hoverStartTime;
        private bool targetIsHovered = false;
        private bool mouseButtonIsDown = false;

        private int currentTargetIndex = 0; // Overall Counter
        private int currentRepetitionIndex = 0;
        private double currentDistanceIndex = 0f;
        private double currentWidthIndex = 0f;

        private string currentTargetID = ""; // Overall Counter
        private string lastTargetID = ""; // Overall Counter

        private Vector2 mouseOnCanvasPos = new Vector2();
        private Vector2 targetOnCanvasPos = new Vector2();
        private Vector2 lastMouseOnCanvasPos = new Vector2();
        private Vector2 lastTargetOnCanvasPos = new Vector2();

        private Vector3 mouseWorldPos = new Vector3();
        private Vector3 targetWorldPos = new Vector3();
        private Vector3 lastMouseWorldPos = new Vector3();
        private Vector3 lastTargetWorldPos = new Vector3();

        private double currentDistance = 0f;
        private double currentWidth = 0f;
        private long trialStartTime;
        private long trialDuration;

        // Assuming you have a list of trial times and endpoint coordinates
        private List<double> trialLoggingMeanTimes = new List<double>(); // Actual trial times
        private List<Vector2> trialLoggingFromPoints = new List<Vector2>(); // Position of the mouse when trial started
        private List<Vector2> trialLoggingToPoints = new List<Vector2>(); // Position of the mouse when target was hit
        private List<Vector2> trialLoggingSelectPoints = new List<Vector2>(); // Positions of the target center points
        private List<bool> trialLoggingErrors = new List<bool>(); // Add true or false hits


        // Assuming you have a list of trial times and endpoint coordinates
        private List<double> summaryLoggingMeanTimes = new List<double>(); // Final aggregate over those means
        private List<double> summaryLoggingIDes = new List<double>(); // Final aggregate over those means
        private List<double> summaryLoggingErrors = new List<double>(); // Final aggregate over those errors
        private List<double> summaryLoggingThroughputs = new List<double>(); // Final aggregate over that throughputs

        AudioSource audioBeep;

        // Start is called before the first frame update
        void Start() {
            Application.targetFrameRate = 90;
            audioBeep = GetComponent<AudioSource>();

            InitializeLogging();
            InitializeCanvas();
            InitializeTargets();
            InitializeCursor();

            StartFittsLawTask();
        }

        private void StartFittsLawTask() {
            trialStartTime = System.DateTime.Now.Ticks;
            ClearTrialData();
            ClearSummaryData();
            UpdateTargets();
        }

        private void InitializeLogging() {
            // Utilizing user-specific local folders for persistent directory storage.
            string applicationPath = Application.persistentDataPath;
            print(applicationPath);

            // Method to create a log file writer.
            StreamWriter CreateLogFileWriter(string logPath, string logType, string header) {
                var absolutePath = Path.Combine(applicationPath, logPath);
                if (!Directory.Exists(absolutePath)) {
                    Directory.CreateDirectory(absolutePath);
                }
                var filePath = Path.Combine(absolutePath, $"{logType}_{SubjectID}_{DateTime.Now.Ticks}.csv");
                var logFileWriter = new StreamWriter(filePath) { AutoFlush = true };
                logFileWriter.WriteLine(header);
                return logFileWriter;
            }

            // Initialize logging for events, movements, and evaluations if enabled.
            if (saveEvents) eventLogFileWriter = CreateLogFileWriter(eventsLogPath, "EventLog", GetEventLogHeader());
            if (saveMovements) movementLogFileWriter = CreateLogFileWriter(movementLogPath, "MovementLog", GetEventLogHeader());
            if (saveEvaluation) evaluationLogFileWriter = CreateLogFileWriter(evalationLogPath, "EvaluationLog", GetFittsEvaluationLogHeader());
            if (saveEvaluation) summaryLogFileWriter = CreateLogFileWriter(evalationLogPath, "SummaryLog", GetGrandMeanResultsLogHeader());
        }


        private void InitializeCanvas() {
            Image backgroundPanelImage = backgroundPanel.GetComponent<Image>();
            backgroundPanelImage.color = backgroundColor;
        }

        private void InitializeCursor() {
            Cursor.visible = !renderCursorOnCanvas; // hides the OS cursor 

            // Create a new GameObject named 'Cursor' and assign it to the 'cursor' variable
            cursor = new GameObject("Cursor");

            // Set the newly created cursor as a child of the screenCanvas, without keeping the world position
            cursor.transform.SetParent(screenCanvas.transform, false);

            // Add an Image component to the cursor GameObject and set its sprite and color
            Image targetImage = cursor.AddComponent<Image>();
            targetImage.sprite = cursorSprite; // Assign the sprite for the cursor
            targetImage.color = cursorColor;  // Set the color of the cursor

            // Get the RectTransform component for setting size and position
            RectTransform cursorRectTransform = cursor.GetComponent<RectTransform>();
            cursorRectTransform.sizeDelta = new Vector2(cursorSize, cursorSize); // Set the size of the cursor
            cursorRectTransform.anchoredPosition = new Vector2(0, 0);      // Initialize the position at (0,0)

            // Set the pivot of the cursor based on the cursorCenter setting
            switch (cursorCenter) {
                case CursorCenter.TopLeft:
                    // If the pivot is set to TopLeft, adjust the pivot to the top-left corner
                    cursorRectTransform.pivot = new Vector2(0, 1);
                    break;
                case CursorCenter.Center:
                    // If the pivot is set to Center, adjust the pivot to the center
                    cursorRectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }

            // Set the visibility of the cursor based on the showCursor flag
            SetGameObjectVisibility(cursor, renderCursorOnCanvas);
        }

        // Update is called once per frame
        void Update() {
            // Check if the current target index is within the bounds of the target list
            if (currentTargetIndex < targetList.Count) {
                // If so, handle the selection logic (e.g., checking for mouse clicks or dwell time)
                HandleTargetSelection();
            }

            // Listen for the 'C' key press to toggle cursor visibility
            if (Input.GetKeyDown(KeyCode.C)) {
                // Toggle the 'showCursor' flag to its opposite value (true becomes false, false becomes true)
                renderCursorOnCanvas = !renderCursorOnCanvas;

                // Set the visibility of the custom cursor GameObject to match the 'showCursor' flag
                SetGameObjectVisibility(cursor, renderCursorOnCanvas);
            }

            // Listen for the 'S' key press to restart the task
            if (Input.GetKeyDown(KeyCode.S)) {
                StartFittsLawTask();
            }
            Cursor.visible = !renderCursorOnCanvas;
        }


        void InitializeTargets() {
            // List to hold all target identifiers for the session
            List<string> targetIDs = new List<string>();

            // Pre-calculate string representations of amplitudes and widths to avoid redundant calculations
            var amplitudeStrings = amplitudes.Select(a => a.ToString()).ToArray();
            var widthStrings = widths.Select(w => w.ToString()).ToArray();

            // Iterate through each repetition to create targets
            for (int repetitionIndex = 0; repetitionIndex < numberOfRepetitions; repetitionIndex++) {
                string baseRepetitionStr = "Target-" + repetitionIndex;
                List<string> amplitudeWidthCombinations = new List<string>();

                // Create all possible amplitude-width combinations
                foreach (var amplitude in amplitudeStrings) {
                    foreach (var width in widthStrings) {
                        amplitudeWidthCombinations.Add(amplitude + "-" + width);
                    }
                }

                // Randomize the order of combinations if required
                if (randomizeTargetConditions) {
                    ShuffleList(amplitudeWidthCombinations, SubjectID);
                }

                // Combine base repetition string with each amplitude-width combination
                targetIDs.AddRange(amplitudeWidthCombinations.Select(combination => $"{baseRepetitionStr}-{combination}"));
            }

            // Caching the swap
            double swap1D = 1.0;

            // Calculate the angle step for placing targets in a circle
            double angleStep = (360.0 / numberOfTrials) / 2 + 180.0;

            // Create targets based on the generated target IDs
            foreach (string targetID in targetIDs) {
                // Split the targetID to extract amplitude and width
                string[] parts = targetID.Split('-');
                double amplitude = double.Parse(parts[2]);
                double width = double.Parse(parts[3]);


                // Iterate through the number of trials to create each target
                for (int targetIndex = 0; targetIndex < numberOfTrials; targetIndex++) {
                    // Construct the unique name for the target
                    string targetName = $"{targetID}_{targetIndex}";
                    // Create a new GameObject with the unique target name
                    GameObject target = new GameObject(targetName);
                    // Set the parent of the target to be the screenCanvas so it's part of the UI hierarchy
                    target.transform.SetParent(screenCanvas.transform, false);

                    // Add an Image component to the target and assign the foreground color
                    Image targetImage = target.AddComponent<Image>();
                    targetImage.color = foregroundColor;

                    // Initialize position variables
                    double x = 0.0, y = 0.0;
                    switch (fittsTaskType) {
                        case TaskType.OneDimensional:
                            // Set the sprite and size for one-dimensional targets
                            targetImage.sprite = targetRectSprite;
                            target.GetComponent<RectTransform>().sizeDelta = new Vector2((float)width, 300);

                            // Calculate the position of the target based on amplitude
                            swap1D = swap1D * -1.0;
                            x = swap1D * (amplitude / 2);
                            y = 0.5;
                            break;

                        case TaskType.TwoDimensional:
                            // Set the sprite and size for two-dimensional targets
                            targetImage.sprite = targetDiscSprite;
                            target.GetComponent<RectTransform>().sizeDelta = new Vector2((float)width, (float)width);

                            // Calculate the angle for the current target in radians
                            double angleRadians = (targetIndex * angleStep + 180.0) * Mathf.Deg2Rad;

                            // Calculate and set the position of the target based on the radius and angle
                            double radius = amplitude / 2;
                            x = Mathf.Cos((float)angleRadians) * radius;
                            y = Mathf.Sin((float)angleRadians) * radius;
                            break;
                    }

                    // Set the position of the target on the canvas
                    target.GetComponent<RectTransform>().anchoredPosition = new Vector2((float)x, (float)y);

                    // Add the target to the list for tracking
                    targetList.Add(target);
                }
            }

            // Store the ID of the last target created for reference
            lastTargetID = targetList[currentTargetIndex].name.Split("_")[0];
        }


        void NextTrial() {
            // Starting the timer
            trialStartTime = System.DateTime.Now.Ticks;

            // beep
            if(audioFeedback) audioBeep.Play(0);

            lastMouseOnCanvasPos = mouseOnCanvasPos;
            lastMouseWorldPos = mouseWorldPos;
            lastTargetOnCanvasPos = targetList[currentTargetIndex].GetComponent<RectTransform>().anchoredPosition;
            lastTargetWorldPos = targetList[currentTargetIndex].transform.position;
            lastTargetID = targetList[currentTargetIndex].name.Split("_")[0];

            // Increment the counter for the next trial
            currentTargetIndex++;

            if (currentTargetIndex >= targetList.Count) {
                EndTask();
                return;
            }

            // Highlight the current target and update the visibility of all targets 
            UpdateTargets();
        }

        private void EndTask() {
            // Write the grand master results file
            if (saveEvaluation) LogFittsSummary();

            // Hide all targets
            foreach (GameObject target in targetList) {
                SetGameObjectVisibility(target, false);
            }
            // Hide the cursor
            SetGameObjectVisibility(cursor, false);

            // Optionally, hide the background panel if it exists
            if (backgroundPanel != null) {
                backgroundPanel.SetActive(false);
            }

            // Close file writers
            CloseFileWriters();

            // Open the file path to find your files
            print("Task Ended. Everything is now hidden. Your results are saved in:");
            print(Application.persistentDataPath);
        }

        private void CloseFileWriters() {
            if (eventLogFileWriter != null) {
                eventLogFileWriter.Close();
                eventLogFileWriter.Dispose();
            }

            if (movementLogFileWriter != null) {
                movementLogFileWriter.Close();
                movementLogFileWriter.Dispose();
            }

            if (evaluationLogFileWriter != null) {
                evaluationLogFileWriter.Close();
                evaluationLogFileWriter.Dispose();
            } 

            if (summaryLogFileWriter != null) {
                summaryLogFileWriter.Close();
                summaryLogFileWriter.Dispose();
            }
        }

        void OnApplicationQuit() {
            CloseFileWriters();
        }

        void UpdateTargets() {
            // Log the current target's name and index for debugging purposes
            Debug.Log("Current Target: " + targetList[currentTargetIndex].name + " (Index: " + currentTargetIndex + ")" + " Last Target: " + lastTargetID);

            // Hide all targets initially, unless showAmplitudeTrials is on
            if (showAmplitudeTrials) {
                for (int i = 0; i < targetList.Count; i++) {
                    // Depending on the task type, determine the visibility of each target
                    switch (fittsTaskType) {
                        case TaskType.OneDimensional:
                            // For OneDimensional tasks, hide all targets initially
                            SetGameObjectVisibility(targetList[i], false);
                            break;
                        case TaskType.TwoDimensional:
                            // For TwoDimensional tasks, show targets with the same ID prefix as the current target
                            SetGameObjectVisibility(targetList[i], (targetList[currentTargetIndex].name.Split("_")[0].Equals(targetList[i].name.Split("_")[0])));
                            break;
                    }
                    // Set the color of all targets to the foreground color
                    SetTargetColor(targetList[i], foregroundColor);
                }
                // Additional logic for specific task types
                switch (fittsTaskType) {
                    case TaskType.OneDimensional:
                        // For OneDimensional tasks, ensure the next target is visible if it exists and it's not the end of a trial set
                        if ((currentTargetIndex + 1) < targetList.Count && currentTargetIndex % numberOfTrials != numberOfTrials - 1)
                            SetGameObjectVisibility(targetList[currentTargetIndex + 1], true);

                        // If it's the last target in a trial set, ensure the previous target is visible
                        if (currentTargetIndex % numberOfTrials == numberOfTrials - 1)
                            SetGameObjectVisibility(targetList[currentTargetIndex - 1], true);

                        break;
                    case TaskType.TwoDimensional:
                        // Specific logic for TwoDimensional tasks can be added here
                        break;
                }
            } else {
                foreach (GameObject target in targetList) {
                    // Hide all targets and set their color to the foreground color
                    SetGameObjectVisibility(target, false);
                    SetTargetColor(target, foregroundColor);
                }

            }
            // Show and color the current target
            SetGameObjectVisibility(targetList[currentTargetIndex], true);
            SetTargetColor(targetList[currentTargetIndex], targetColor);
        }


        void SetGameObjectVisibility(GameObject gameObject, bool isVisible) {
            // Check if the target has an Image component (for UI elements)
            Image targetImage = gameObject.GetComponent<Image>();
            if (targetImage != null) {
                targetImage.enabled = isVisible;
            } else {
                // If the target is a non-UI GameObject, enable/disable the GameObject itself
                gameObject.SetActive(isVisible);
            }
        }

        static void ShuffleList<T>(List<T> list, int seed) {
            // Initialize the random number generator with the given seed
            System.Random rand = new System.Random(seed);

            // Fisher-Yates shuffle algorithm
            for (int i = list.Count - 1; i > 0; i--) {
                // Select a random index before the current position
                int j = rand.Next(i + 1);

                // Swap the elements at the current position and the random position
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        void SetTargetColor(GameObject target, Color color) {
            if (target != null) {
                Image targetImage = target.GetComponent<Image>();
                if (targetImage != null) {
                    targetImage.color = color;
                }
            }
        }

        private Vector2 GetMousePositionOnCanvas() {
            // Check if the canvas is in Screen Space Overlay or Screen Space Camera mode
            if (screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay || screenCanvas.renderMode == RenderMode.ScreenSpaceCamera) {
                // Convert the screen point to a local point in the rectangle and assign it to mouseOnCanvasPos
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    screenCanvas.GetComponent<RectTransform>(), // Get the RectTransform component of the canvas
                    Input.mousePosition, // Current mouse position
                    screenCanvas.renderMode == RenderMode.ScreenSpaceCamera ? screenCanvas.worldCamera : null, // Use the world camera if in Screen Space Camera mode
                    out mouseOnCanvasPos // Output the local point
                );
            } else if (screenCanvas.renderMode == RenderMode.WorldSpace) {
                // If the canvas is in World Space mode

                // Create a ray from the mouse position on the screen
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                float enter; // Variable to store the distance along the ray where it intersects the plane

                // Create a plane at the canvas position with the same normal as the canvas
                Plane plane = new Plane(screenCanvas.transform.forward, screenCanvas.transform.position);

                // If the ray hits the plane...
                if (plane.Raycast(ray, out enter)) {
                    // Calculate the hit point where the ray intersects the plane
                    Vector3 hitPoint = ray.GetPoint(enter);

                    // Convert the hit point to local position in the canvas and assign it to mouseOnCanvasPos
                    mouseOnCanvasPos = screenCanvas.transform.InverseTransformPoint(hitPoint);
                }
            }

            // Return the calculated mouse position on the canvas
            return mouseOnCanvasPos;
        }


        void HandleTargetSelection() {
            // Determine if the target ID will change in the next index
            bool targetIDChange = (currentTargetIndex + 1) < targetList.Count
              ? !targetList[currentTargetIndex].name.Split("_")[0].Equals(targetList[currentTargetIndex + 1].name.Split("_")[0])
              : true;

            // Determine the duration
            trialDuration = System.DateTime.Now.Ticks - trialStartTime;
            currentRepetitionIndex = int.Parse(targetList[currentTargetIndex].name.Split("_")[0].Split("-")[1]);
            currentDistanceIndex = double.Parse(targetList[currentTargetIndex].name.Split("_")[0].Split("-")[2]);
            currentWidthIndex = double.Parse(targetList[currentTargetIndex].name.Split("_")[0].Split("-")[3]);

            // Calculate the position of the current target and the mouse on the canvas
            targetOnCanvasPos = targetList[currentTargetIndex].GetComponent<RectTransform>().anchoredPosition;
            targetWorldPos = targetList[currentTargetIndex].transform.position;

            mouseOnCanvasPos = GetMousePositionOnCanvas();
            mouseWorldPos = cursor.transform.position;

            // Calculate the current distance and width for hover detection
            currentDistance = Vector2.Distance(mouseOnCanvasPos, targetOnCanvasPos);
            currentWidth = double.Parse(targetList[currentTargetIndex].name.Split("_")[0].Split("-")[3]) / 2.0;
            currentTargetID = targetList[currentTargetIndex].name.Split("_")[0];

            // Update the cursor's position to follow the mouse
            cursor.GetComponent<RectTransform>().anchoredPosition = mouseOnCanvasPos;

            // Initialize targetIsHovered as false and check mouse button state
            targetIsHovered = false;
            mouseButtonIsDown = Input.GetMouseButtonDown(0);

            // Movement logging if enabled
            if (saveMovements) LogEvent(movementLogFileWriter);

            // Handle selection based on the task type
            switch (fittsTaskType) {
                case TaskType.OneDimensional:
                    // Calculate the left, right, top, and bottom bounds of the target and determine if the mouse is within the bounds of the target
                    RectTransform targetRect = targetList[currentTargetIndex].GetComponent<RectTransform>();
                    if (mouseOnCanvasPos.x >= (targetOnCanvasPos.x - targetRect.sizeDelta.x / 2) && mouseOnCanvasPos.x <= (targetOnCanvasPos.x + targetRect.sizeDelta.x / 2) &&
                      mouseOnCanvasPos.y <= (targetOnCanvasPos.y + targetRect.sizeDelta.y / 2) && mouseOnCanvasPos.y >= (targetOnCanvasPos.y - targetRect.sizeDelta.y / 2)) {
                        targetIsHovered = true;
                    }
                    break;

                case TaskType.TwoDimensional:
                    // For TwoDimensional tasks, use the distance method 
                    targetIsHovered = (currentDistance <= currentWidth);
                    break;

            }

            // Change the target's color if it's being hovered over, depending on the mouseOverHighlight setting
            if (targetIsHovered && mouseOverHighlight) SetTargetColor(targetList[currentTargetIndex], mouseOverColor);
            else SetTargetColor(targetList[currentTargetIndex], targetColor);

            // Handle selection based on the specified selection method
            switch (taskSelectionMethod) {
                case SelectionMethod.MouseButton:
                    if (Input.GetMouseButtonDown(0)) {
                        trialLoggingErrors.Add(targetIsHovered);
                        if (targetIsHovered) AddTrialData();
                        if (saveEvents) LogEvent(eventLogFileWriter);
                        if (saveEvaluation && targetIDChange) {
                            if (targetIDChange && targetIsHovered) {
                                LogFittsEvaluationResults();
                                ClearTrialData();
                            }
                        }
                        if (targetIsHovered) NextTrial();
                    }
                    break;

                case SelectionMethod.DwellTime:
                    if (targetIsHovered) {
                        if (hoverStartTime == default) {
                            hoverStartTime = DateTime.Now;
                        } else if ((DateTime.Now - hoverStartTime).TotalMilliseconds >= dwellTime) {
                            hoverStartTime = default;
                            AddTrialData();
                            if (saveEvents) LogEvent(eventLogFileWriter);
                            if (saveEvaluation && targetIDChange) {
                                if (targetIDChange) {
                                    LogFittsEvaluationResults();
                                    ClearTrialData();
                                }
                            }
                            NextTrial();
                        }
                    } else {
                        hoverStartTime = default;
                    }
                    break;
            }
        }

        // Helper method to add trial data
        private void AddTrialData() {
            trialLoggingMeanTimes.Add(trialDuration);
            trialLoggingFromPoints.Add(lastMouseOnCanvasPos);
            trialLoggingToPoints.Add(targetOnCanvasPos);
            trialLoggingSelectPoints.Add(mouseOnCanvasPos);
        }

        // Helper method to clear trial data
        private void ClearTrialData() {
            trialLoggingMeanTimes.Clear();
            trialLoggingFromPoints.Clear();
            trialLoggingToPoints.Clear();
            trialLoggingSelectPoints.Clear();
            trialLoggingErrors.Clear();
        }

        // Clear everything 
        private void ClearSummaryData() {
            summaryLoggingMeanTimes.Clear();
            summaryLoggingIDes.Clear();
            summaryLoggingErrors.Clear();
            summaryLoggingThroughputs.Clear();
        }

        private string GetEventLogHeader() {
            return "Timestamp," +
                "SubjectID," +
                "Condition," +
                "Task," +
                "Group," +
                "FittsTask," +
                "SelectionMethod," +
                "Repetition," +
                "Amplitude," +
                "Width," +
                "CurrentTargetIndex," +
                "TargetName," +
                "IndexOfDifficulty," +
                "TrialDuration," +
                "LastTargetPosX," +
                "LastTargetPosY," +
                "CurrentTargetPosX," +
                "CurrentTargetPosY," +
                "MousePosX," +
                "MousePosY," +
                "Distance," +
                "TargetSize," +
                "MouseButtonDown," +
                "TargetWasHovered," +
                "LastTargetWorldX," +
                "LastTargetWorldY," +
                "LastTargetWorldZ," +
                "CurrentTargetWorldX," +
                "CurrentTargetWorldY," +
                "CurrentTargetWorldZ," +
                "MouseWorldPosX," +
                "MouseWorldPosY," +
                "MouseWorldPosZ";
        }

        private void LogEvent(StreamWriter fileWriter) {
            // Currently used for Event and Movement Output. For more movement output (camera, controller, etc.) clone this function and add your stuff
            string fileOutput =
              System.DateTime.Now.Ticks + "," +
              SubjectID + "," +
              Condition + "," +
              Task + "," +
              Group + "," +
              fittsTaskType.ToString() + "," +
              taskSelectionMethod.ToString() + "," +
              currentRepetitionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              currentDistanceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              currentWidthIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              currentTargetIndex + "," +
              targetList[currentTargetIndex].name + "," +
              Math.Round(Math.Log((currentDistanceIndex / currentWidthIndex) + 1f, 2), 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              trialDuration.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              lastTargetOnCanvasPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              lastTargetOnCanvasPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              targetOnCanvasPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              targetOnCanvasPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              mouseOnCanvasPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              mouseOnCanvasPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              currentDistance.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              currentWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              mouseButtonIsDown + "," +
              targetIsHovered + "," +
              lastTargetWorldPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              lastTargetWorldPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              lastTargetWorldPos.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              targetWorldPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              targetWorldPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              targetWorldPos.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              mouseWorldPos.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              mouseWorldPos.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
              mouseWorldPos.z.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // print(fileOutput);
            fileWriter.WriteLine(fileOutput);
        }

        private string GetFittsEvaluationLogHeader() {
            return "Timestamp," +
                "SubjectID," +
                "Condition," +
                "Task," +
                "Group," +
                "FittsTaskType," +
                "SelectionMethod," +
                "NumberOfRepetitions," +
                "NumberOfTrials," +
                "IndexOfDifficulty," +
                "EffectiveAmplitude," +
                "EffectiveWidth," +
                "EffectiveIndexOfDifficulty," +
                "MeanTimeInSeconds," +
                "TotalErrors," +
                "ErrorRate," +
                "Throughput";
        }

        private void LogFittsEvaluationResults() { 
            // Calculate cumulative Ae and prepare deviations list
            List<double> dxs = new List<double>();
            List<double> aes = new List<double>();

            for (int i = 0; i < trialLoggingMeanTimes.Count; i++) {
                // Extract the coordinates for the current trial
                double x1 = trialLoggingFromPoints[i].x;
                double y1 = trialLoggingFromPoints[i].y;
                double x2 = trialLoggingToPoints[i].x;
                double y2 = trialLoggingToPoints[i].y;
                double x = trialLoggingSelectPoints[i].x;
                double y = trialLoggingSelectPoints[i].y;

                // Calculate the lengths of the sides of the triangle
                double a = Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
                double b = Math.Sqrt(Math.Pow(x - x2, 2) + Math.Pow(y - y2, 2));
                double c = Math.Sqrt(Math.Pow(x1 - x, 2) + Math.Pow(y1 - y, 2));

                // Calculate dx
                double dx = (c * c - b * b - a * a) / (2.0 * a);

                // Calculate the effective target amplitude (Ae) 
                double ae = a + dx;

                // That's what we need
                aes.Add(ae); 
                dxs.Add(dx);
            }

            // Calculate Effective Width (We), Mean Ae, and Mean Time (MT) 
            double effectiveWidth = 4.133 * StandardDeviation(dxs); // We'll never figure out why this is it
            double meanAe = aes.Average();
            double meanTime = trialLoggingMeanTimes.Average() / 10000000.0; // I love ticks

            // Compute the number of errors and error rate
            int errors = trialLoggingErrors.Count(hit => !hit);
            double errorRate = errors / (double)trialLoggingErrors.Count;

            // Compute Throughput (TP) according to https://www.yorku.ca/mack/hcii2015a.html
            double effectiveIndexOfDifficulty = Math.Log((meanAe / effectiveWidth) + 1, 2);
            double throughput = effectiveIndexOfDifficulty / meanTime;

            // For the summary computation
            summaryLoggingMeanTimes.Add(meanTime);
            summaryLoggingIDes.Add(effectiveIndexOfDifficulty);
            summaryLoggingErrors.Add(errorRate);
            summaryLoggingThroughputs.Add(throughput);

            // Prepare the file output
            string fileOutput = System.DateTime.Now.Ticks + "," +
                                SubjectID + "," +
                                Condition + "," +
                                Task + "," +
                                Group + "," +
                                fittsTaskType.ToString() + "," +
                                taskSelectionMethod.ToString() + "," +
                                numberOfRepetitions + "," +
                                NumberOfTrials + "," +
                                Math.Round(Math.Log((currentDistance / currentWidth) + 1f, 2), 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                                meanAe.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                effectiveWidth.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                effectiveIndexOfDifficulty.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                meanTime.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                errors + "," + // Insert the actual number of errors
                                errorRate.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                throughput.ToString("F8", System.Globalization.CultureInfo.InvariantCulture);

            // Output and log the results
            print(fileOutput);
            evaluationLogFileWriter.WriteLine(fileOutput);
        } 
        // Helper method to calculate standard deviation
        private double StandardDeviation(List<double> values) {
            double mean = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - mean) * (val - mean)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
        }

        private string GetGrandMeanResultsLogHeader() {
            return "Timestamp," +
                "SubjectID," +
                "Condition," +
                "Task," +
                "Group," +
                "FittsTask," +
                "SelectionMethod," +
                "Repetitions," +
                "Trials," +
                "IDe," +
                "MeanTime," +
                "ErrorRate," +
                "Throughput," +
                "RegressionEquation," +
                "RegressionFit";
        }

        private void LogFittsSummary() {
            // Prepare the output for the final summary of a task 
            double meanTime = summaryLoggingMeanTimes.Average();
            double meanErrorRate = summaryLoggingErrors.Average();
            double meanThroughput = summaryLoggingThroughputs.Average();

            string fileOutput = System.DateTime.Now.Ticks + "," +
                                SubjectID + "," +
                                Condition + "," +
                                Task + "," +
                                Group + "," +
                                fittsTaskType.ToString() + "," +
                                taskSelectionMethod.ToString() + "," +
                                numberOfRepetitions + "," +
                                NumberOfTrials + "," +
                                Math.Round(Math.Log((currentDistance / currentWidth) + 1f, 2), 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                                meanTime.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                meanErrorRate.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                meanThroughput.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                CalculateLinearRegression(summaryLoggingIDes, summaryLoggingMeanTimes);

            // Output and log the results
            print(fileOutput);
            summaryLogFileWriter.WriteLine(fileOutput);
        }

        private string CalculateLinearRegression(List<double> xVals, List<double> yVals) {
            if (xVals.Count != yVals.Count)
                throw new ArgumentException("The number of x and y values must be equal.");

            int n = xVals.Count;
            double sumX = xVals.Sum();
            double sumY = yVals.Sum();
            double sumXx = xVals.Select(x => x * x).Sum();
            double sumYy = yVals.Select(y => y * y).Sum();
            double sumXy = xVals.Zip(yVals, (x, y) => x * y).Sum();

            double slope = (n * sumXy - sumX * sumY) / (n * sumXx - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;

            double ssTot = yVals.Select(y => Math.Pow(y - yVals.Average(), 2)).Sum();
            double ssRes = yVals.Zip(xVals, (y, x) => Math.Pow(y - (slope * x + intercept), 2)).Sum();
            double rSquared = 1 - (ssRes / ssTot);

            return $"y = {slope.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)}x + {intercept.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)}, R² = {rSquared.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}