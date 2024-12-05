var timeOutMessage = setTimeout(() => {
    $('#errorMessage').hide();
}, 5000);

// Script to send data with jQuery
$(document).ready(() => {
    // Add back button click handler
    $('#back').on('click', function() {
        // Use the configured URL from the view
        window.location.href = backButtonUrl;
    });

    $('#resetPasswordBtn').on('click', function () {
        // Capture the form values
        var newPassword = $('#newPassword').val();
        var confirmPassword = $('#confirmPassword').val();
        var token = $('#token').val();
        // Verify if both fields are completed
        if (newPassword === "" || confirmPassword === "") {
            $('#errorMessage').text("Favor de insertar su nueva contraseña").show();
            timeOutMessage
            return;
        }
        if (newPassword !== confirmPassword) {
            $('#errorMessage').text("Las contraseñas no coinciden.").show();
            timeOutMessage
            return;
        }
        // Make the request using jQuery.ajax
        $.ajax({
            url: '/api/auth/resetPassword',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                token: token,
                newPassword: newPassword
            }),
            success: function (response) {
                // Hide the form and show the success message
                $('#resetPasswordForm').hide();
                $('#successMessage').show();
            },
            error: function (xhr, status, error) {
                // Show error message
                var errorMessage = xhr.responseJSON?.title || 'Error al restablecer la contraseña';
                alert(errorMessage);
            }
        });
    });
});