# Fitts' Law Task for Unity

This Unity project is designed to conduct experiments based on Fitts' Law, a predictive model of human movement primarily used in human-computer interaction and ergonomics. This law predicts the time required to rapidly move to a target area, such as in the case of a mouse cursor moving to a button on the screen. The project allows for a variety of settings and customizations to suit different experimental needs.

[![Watch the video](https://youtu.be/e69nWZy3qBI)](https://youtu.be/e69nWZy3qBI)

## Features

- **Task Types**: Supports OneDimensional and TwoDimensional tasks, allowing for a range of experiments from simple linear movements to more complex 2D tasks.
- **Selection Methods**: Includes MouseButton and DwellTime selection methods, catering to different interaction styles and accessibility needs.
- **Customizable Settings**: Offers a wide range of settings, including the number of trials, target sizes, amplitudes, and more, to tailor the task to specific research requirements.
- **Visual Customization**: Provides options to customize the appearance of targets and the cursor, including colors and sprites, to enhance the user experience or match the study's design.
- **Logging**: Features comprehensive logging capabilities, capturing detailed event, movement, and evaluation data for thorough analysis.
- **Performance Metrics**: Calculates and logs various performance metrics such as mean times, error rates, and throughput, providing valuable insights into user performance and behavior.

## Getting Started

### Prerequisites

- Unity Editor (Version 2019.4 or later recommended)
- Basic understanding of Unity and C#

### Installation

2. **Open the project in Unity:**
   - Launch Unity Hub.
   - Click on 'Add' and select the cloned/downloaded project directory.

3. **Explore the project:**
   - Navigate through the assets and scenes to familiarize yourself with the structure.

### Usage

1. **Configure the Task:**
   - Select the `FittsTask` GameObject in the hierarchy.
   - Adjust the settings in the Inspector to set up your desired task parameters.

2. **Run the Experiment:**
   - Press the Play button in Unity to start the experiment.
   - Interact with the task as per the configured settings.

3. **Review the Logs:**
   - Find the logs in the specified paths within your project directory or the designated output directory.

## Customization

- **Task Settings**: Customize the task type, selection method, number of trials, and more through the Inspector.
- **Visuals**: Change the sprites, colors, and sizes of targets and the cursor to match your study's needs.
- **Logging**: Enable or disable different types of logging and specify paths for the output files.

## Contributing

Contributions to enhance or expand the project are welcome! Please feel free to fork the repository, make your changes, and submit a pull request.

## License

This project is open-source and available under the [MIT License](LICENSE.md).

## Acknowledgments
 
- The tool is based on Scott MacKenzies implementation of GoFitts and https://www.yorku.ca/mack/hcii2015a.html

---

For more details on how to use, customize, and contribute to this project, please refer to the individual files and comments within the project. This README provides just a summary and starting point for using and understanding the Fitts' Law Task for Unity.
