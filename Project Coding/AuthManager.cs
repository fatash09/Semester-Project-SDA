using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class AuthManager : MonoBehaviour
{
    [Header("Login Fields")]
    public InputField loginEmailInput;
    public InputField loginPasswordInput;
    public GameObject loginWindow;

    [Header("Registration Fields")]
    public InputField registerEmailInput;
    public InputField registerPasswordInput;
    public InputField confirmPasswordInput;
    public GameObject registrationWindow;

    [Header("OTP Verification Fields")]
    public InputField otpInputField; // Field for entering OTP
    public GameObject otpVerificationWindow;

    [Header("Loading Indicators")]
    public GameObject loginLoading;
    public GameObject signupLoading;

    [Header("Dialog Box Elements")]
    public GameObject dialogBox;
    public Text dialogTitle;
    public Text dialogInfo;
    public Button dialogOkButton;

    [Header("Navigation Buttons")]
    public GameObject forgotPasswordButton;
    public GameObject goToLoginButton;
    public GameObject createAccountButton;

    private int loginAttempts = 0;
    private FirebaseAuth auth;
    private FirebaseFirestore db;

    private string dialogContext = "";
    private string currentOtp = ""; // Store the generated OTP

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;

        dialogBox.SetActive(false);
        ShowLoginWindow();

        otpVerificationWindow.SetActive(false); // Hide OTP verification window initially

        if (dialogOkButton != null)
            dialogOkButton.onClick.AddListener(OnDialogOkClicked);
    }

    void OnEnable()
    {
        otpVerificationWindow.SetActive(false); // Ensure OTP verification window is hidden when scene starts
    }

    public void ShowLoginWindow()
    {
        loginWindow.SetActive(true);
        registrationWindow.SetActive(false);
        otpVerificationWindow.SetActive(false); // Hide OTP verification window during login

        forgotPasswordButton.SetActive(false);
        goToLoginButton.SetActive(false);
        createAccountButton.SetActive(true);
    }

    public void ShowRegisterWindow()
    {
        loginWindow.SetActive(false);
        registrationWindow.SetActive(true);
        otpVerificationWindow.SetActive(false); // Hide OTP verification window during registration

        goToLoginButton.SetActive(true);
        forgotPasswordButton.SetActive(false);
        createAccountButton.SetActive(false);
    }

    public void ShowOtpVerificationWindow()
    {
        loginWindow.SetActive(false);
        registrationWindow.SetActive(false);
        otpVerificationWindow.SetActive(true); // Show OTP verification window

        forgotPasswordButton.SetActive(false);
        goToLoginButton.SetActive(false);
        createAccountButton.SetActive(false);
    }

    public void SignIn()
    {
        string email = loginEmailInput.text.Trim();
        string password = loginPasswordInput.text;
        loginLoading.SetActive(true);

        // Step 1: Ensure both email and password are filled
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            loginLoading.SetActive(false);
            ShowDialog("Login Failed", "Email and password are required.", "login_error");
            return;
        }

        // Step 2: Validate email format
        if (!IsValidEmail(email))
        {
            loginLoading.SetActive(false);
            ShowDialog("Login Failed", "Invalid email format.", "login_error");
            return;
        }

        // Step 3: Authenticate the user with Firebase
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            loginLoading.SetActive(false);

            if (task.IsCanceled || task.IsFaulted)
            {
                ShowDialog("Login Failed", "Incorrect credentials. Try again or reset password.", "login_error");
                return;
            }

            FirebaseUser user = task.Result.User;
            Debug.Log("✅ Login successful: " + user.Email);
            SceneManager.LoadScene("ARScene");
        });
    }

    public void SignUp()
    {
        string email = registerEmailInput.text.Trim();
        string password = registerPasswordInput.text;
        string confirmPassword = confirmPasswordInput.text;

        signupLoading.SetActive(true);

        // Step 1: Ensure all fields are filled
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            signupLoading.SetActive(false);
            ShowDialog("SIGN UP FAILED", "All fields are required.", "signup_error");
            return;
        }

        // Step 2: Validate email format
        if (!IsValidEmail(email))
        {
            signupLoading.SetActive(false);
            ShowDialog("SIGN UP FAILED", "Invalid email format.", "signup_error");
            return;
        }

        if (IsEmailStartingWithNumber(email))
        {
            signupLoading.SetActive(false);
            ShowDialog("SIGN UP FAILED", "Email cannot start with a number.", "signup_error");
            return;
        }

        // Step 3: Validate password length
        if (password.Length < 6)
        {
            signupLoading.SetActive(false);
            ShowDialog("SIGN UP FAILED", "Password must be at least 6 characters.", "signup_error");
            return;
        }

        // Step 4: Ensure password and confirm password match
        if (password != confirmPassword)
        {
            signupLoading.SetActive(false);
            ShowDialog("SIGN UP FAILED", "Passwords do not match.", "signup_error");
            return;
        }

        // Step 5: Proceed with Firebase user creation
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            signupLoading.SetActive(false);

            if (task.IsCanceled || task.IsFaulted)
            {
                ShowDialog("SIGN UP FAILED", task.Exception?.InnerException?.Message ?? "Signup failed.", "signup_error");
                return;
            }

            FirebaseUser newUser = task.Result.User;

            // Save user data to Firestore
            Dictionary<string, object> userData = new Dictionary<string, object>
            {
                { "email", email },
                { "created_at", Timestamp.GetCurrentTimestamp() }
            };

            db.Collection("users").Document(newUser.UserId).SetAsync(userData).ContinueWithOnMainThread(storeTask =>
            {
                if (storeTask.IsFaulted || storeTask.IsCanceled)
                {
                    ShowDialog("SIGN UP FAILED", "Could not store user data.", "signup_error");
                }
                else
                {
                    Debug.Log("User data stored successfully in Firestore.");
                    // Generate OTP and send it to the user
                    currentOtp = GenerateOTP();
                    SendOtpToEmail(email); // Send OTP to email
                    ShowOtpVerificationWindow(); // Show OTP verification window
                }
            });
        });


    }
    private bool IsEmailStartingWithNumber(string email)
    {
        // Check if the email starts with a number using regex
        return System.Text.RegularExpressions.Regex.IsMatch(email, @"^\d");
    }
    private bool IsValidEmail(string email)
    {
        string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern);
    }

    private string GenerateOTP()
    {
        System.Random random = new System.Random();
        return random.Next(100000, 999999).ToString(); // Generate 6-digit OTP
    }

    private void SendOtpToEmail(string email)
    {
        // Send the OTP email (using SendGrid or your preferred method)
        // For simplicity, we'll log it for now.
        Debug.Log("Sending OTP to: " + email + " OTP: " + currentOtp);
        // After sending the email, you can store the OTP in Firestore if you wish.
    }

    public void VerifyOTP()
    {
        string enteredOtp = otpInputField.text;

        // Step 1: Check if entered OTP matches the generated OTP
        if (enteredOtp == currentOtp)
        {
            // Only store data if OTP matches
            string email = registerEmailInput.text.Trim();
            FirebaseUser newUser = auth.CurrentUser;

            // Save user data to Firestore only after OTP verification
            Dictionary<string, object> userData = new Dictionary<string, object>
            {
                { "email", email },
                { "created_at", Timestamp.GetCurrentTimestamp() }
            };

            db.Collection("users").Document(newUser.UserId).SetAsync(userData).ContinueWithOnMainThread(storeTask =>
            {
                if (storeTask.IsFaulted || storeTask.IsCanceled)
                {
                    ShowDialog("SIGN UP FAILED", "Could not store user data.", "signup_error");
                }
                else
                {
                    Debug.Log("User data stored successfully in Firestore.");
                    ShowDialog("SIGN UP SUCCESS", "OTP verified successfully! Please log in.", "signup_success");
                }
            });
        }
        else
        {
            ShowDialog("SIGN UP FAILED", "Invalid OTP. Try again.", "signup_error");
        }
    }

    private void ShowDialog(string title, string message, string context)
    {
        dialogTitle.text = title;
        dialogInfo.text = message;
        dialogContext = context;

        dialogBox.SetActive(true);
        loginWindow.SetActive(false);
        registrationWindow.SetActive(false);
        otpVerificationWindow.SetActive(false);
    }

    public void OnDialogOkClicked()
    {
        Debug.Log("OK Button Clicked – Dialog Context: " + dialogContext);

        dialogBox.SetActive(false);

        if (dialogContext == "signup_success")
        {
            ShowLoginWindow();
        }
        else if (dialogContext == "signup_error")
        {
            ShowRegisterWindow();
        }
        else if (dialogContext == "login_error")
        {
            ShowLoginWindow();
        }

        dialogContext = "";
    }
}
