# 📈 NinjaScript Indicators Guide

## 🎯 Purpose of This Repository

This repository serves as a comprehensive resource for developers looking to create custom indicators in NinjaScript for NinjaTrader 8. It provides a collection of well-documented indicators, best practices, and a clear learning path, from beginner to advanced levels.

The goal is to offer a hands-on guide that goes beyond official documentation by providing practical, high-quality examples that you can use and learn from.

## 🚀 Getting Started

### Prerequisites

- **NinjaTrader 8**: You must have NinjaTrader 8 installed. You can download it from the [official website](https://ninjatrader.com/).
- **Visual Studio**: For a better development experience, it is recommended to use Visual Studio. You can download the free Community Edition [here](https://visualstudio.microsoft.com/vs/community/).

### Setup and Installation

1.  **Download the Indicators**: Clone or download this repository to your local machine.
2.  **Open NinjaTrader 8**: Launch the NinjaTrader 8 platform.
3.  **Import the Indicators**:
    *   From the NinjaTrader Control Center, select **Tools > Import > NinjaScript Add-On**.
    *   Navigate to the location where you downloaded the repository, select the root folder, and click **Import**.
    *   NinjaTrader will compile the scripts, and upon successful compilation, the indicators will be available in your platform.

## 🛠️ Usage

### Adding an Indicator to a Chart

1.  **Open a Chart**: From the Control Center, go to **New > Chart**.
2.  **Select an Instrument**: Choose the financial instrument and chart settings you want to use.
3.  **Add the Indicator**:
    *   Right-click on the chart and select **Indicators**.
    *   In the Indicators window, find the desired indicator from the list (e.g., "Initial Balance").
    *   Configure the indicator's parameters on the right-hand side.
    *   Click **OK** to add the indicator to your chart.

## 📁 Repository Structure

```
ninjaScript-indicators-guide/
├── README.md
├── docs/
│   ├── ... (further documentation)
├── examples/
│   ├── ... (example indicators)
├── Pruebas/
│   └── InitialBalance.cs
└── ... (other folders)
```

-   **docs/**: Contains detailed documentation on various NinjaScript topics.
-   **examples/**: A collection of example indicators categorized by complexity.
-   **Pruebas/**: Contains the fully documented `InitialBalance.cs` indicator, which serves as a primary example of best practices in documentation and coding.

---

## 🚀 Featured Example: Initial Balance Indicator

### 📂 `Pruebas/InitialBalance.cs`

This is a powerful, fully documented indicator that calculates and displays the Initial Balance (IB) and its extensions. It serves as a great example of:

-   **XML Docstrings**: Every public class, method, and property is documented.
-   **Customizable Parameters**: A wide range of settings that can be configured from the UI.
-   **Advanced Drawing**: Use of `Draw` methods and `SharpDX` for rendering.

You can find the full source code [here](./Pruebas/InitialBalance.cs).

---

## 🤝 Contributing

We welcome contributions! If you have an indicator you'd like to share or improvements to the documentation, please follow these steps:

1.  **Fork** the repository.
2.  **Create a new branch** for your feature or bugfix.
3.  **Add your changes** and ensure they are well-documented.
4.  **Create a pull request** with a clear description of your changes.

---

## 📄 License

This repository is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.
