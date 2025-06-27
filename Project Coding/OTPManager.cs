using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Firestore;
using UnityEngine.Networking;
using System.Text;
using Firebase.Extensions;
using System.Collections.Generic;

public class OTPManager : MonoBehaviour
{
    [Header("SendGrid Configuration")]
    public string sendGridApiKey = "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";  // Use secure methods for storing API key
    public string senderEmail = "fatashabbassi888@gmail.com"; // Verified email in SendGrid

    FirebaseFirestore db;

    [Header("UI Elements")]
    public InputField emailInputField; // Email input
    public InputField otpInputField;   // OTP input
    public Text messageText;           // Text to display messages
    public Button sendOtpButton;       // Button to send OTP
    public Button verifyOtpButton;     // Button to verify OTP

    void Start()
    {
        db = FirebaseFirestore.DefaultInstance;

        // Button click listeners
        //   sendOtpButton.onClick.AddListener(OnSendOtpButtonClicked);
        verifyOtpButton.onClick.AddListener(OnVerifyOtpButtonClicked);
    }

    // Called when Send OTP button is clicked
    //private void OnSendOtpButtonClicked()
    // {
    //  string email = emailInputField.text;

    //  if (IsValidEmail(email))
    //  {
    //    SendOtpToEmail(email);  // Send OTP to the provided email
    // }
    //  else
    //  {
    //  messageText.text = "Please enter a valid email.";
    //  }
    // }

    // Called when Verify OTP button is clicked
    private void OnVerifyOtpButtonClicked()
    {
        string email = emailInputField.text;
        string enteredOtp = otpInputField.text;

        FetchOTPAndVerify(email, enteredOtp);  // Fetch and verify OTP
    }

    // Function to send OTP email
    public void SendOtpToEmail(string recipientEmail)
    {
        string otp = GenerateOTP();
        StoreOtpInFirestore(recipientEmail, otp);
        StartCoroutine(SendEmail(recipientEmail, otp));
    }

    // Function to fetch OTP from Firestore and verify
    public void FetchOTPAndVerify(string email, string enteredOtp)
    {
        DocumentReference docRef = db.Collection("otp_codes").Document(email);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    string storedOtp = snapshot.GetValue<string>("otp");

                    if (storedOtp == enteredOtp)
                    {
                        messageText.text = "OTP Verified Successfully!";
                    }
                    else
                    {
                        messageText.text = "Invalid OTP. Try again.";
                    }
                }
                else
                {
                    messageText.text = "No OTP found for this email.";
                }
            }
            else
            {
                messageText.text = "Failed to fetch OTP.";
            }
        });
    }

    // Generate a 6-digit OTP
    private string GenerateOTP()
    {
        System.Random random = new System.Random();
        return random.Next(100000, 999999).ToString(); // Generate 6-digit OTP
    }

    // Store OTP in Firestore with timestamp
    private IEnumerator DelayOTPVerification(string email, string enteredOtp)
    {
        yield return new WaitForSeconds(1f); // Wait for 1 second before verifying OTP
        FetchOTPAndVerify(email, enteredOtp);
    }

    // Send OTP email using SendGrid
    IEnumerator SendEmail(string toEmail, string otp)
    {
        string jsonBody = "{ \"personalizations\": [{ \"to\": [{ \"email\": \"" + toEmail + "\" }] }]," +
                          "\"from\": { \"email\": \"" + senderEmail + "\" }," +
                          "\"subject\": \"Your OTP Code\"," +
                          "\"content\": [{ \"type\": \"text/plain\", \"value\": \"Your OTP is: " + otp + "\" }] }";

        UnityWebRequest request = new UnityWebRequest("https://api.sendgrid.com/v3/mail/send", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + sendGridApiKey);
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            messageText.text = "OTP email sent successfully!";
        }
        else
        {
            messageText.text = "Failed to send OTP: " + request.error;
        }
    }
    private void StoreOtpInFirestore(string email, string otp)
    {
        DocumentReference docRef = db.Collection("otp_codes").Document(email);

        // Log the path to see if the document is being stored in the correct path
        Debug.Log("Storing OTP for email: " + email + " at path: " + docRef.Path);

        Dictionary<string, object> otpData = new Dictionary<string, object>
    {
        { "otp", otp },
        { "timestamp", FieldValue.ServerTimestamp }
    };

        // Store OTP in Firestore
        docRef.SetAsync(otpData).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
            {
                Debug.Log("OTP successfully stored in Firestore.");
            }
            else
            {
                Debug.LogError("Failed to store OTP in Firestore: " + task.Exception?.Message);
            }
        });
    }
    // Basic email validation
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
