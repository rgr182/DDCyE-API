var selectedGender = null;
const months = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

const ERROR_MESSAGES = {
    NAME_REQUIRED: "Por favor, ingrese su nombre.",
    LASTNAME_REQUIRED: "Por favor, ingrese su apellido.",
    EMAIL_REQUIRED: "Por favor, ingrese su dirección de correo electrónico.",
    EMAIL_INVALID: "Por favor, ingrese una dirección de correo electrónico válida.",
    PASSWORD_REQUIRED: "Favor de insertar su contraseña",
    PASSWORDS_MISMATCH: "Las contraseñas no coinciden.",
    EMAIL_ALREADY_REGISTERED: "Este correo electrónico ya está registrado.",
    INVALID_INPUT: "La contrasena debe tener al menos 8 caracteres.",
    REGISTRATION_FAILED: "No se pudo completar el registro. Por favor, inténtelo de nuevo más tarde.",
    DEFAULT: "Ocurrió un error inesperado. Por favor, inténtelo de nuevo."
};

function selectGender(selectedButton) {
    var buttons = document.querySelectorAll('.gender');
    buttons.forEach(function (button) {
        button.classList.remove('selected');
    });
    selectedButton.classList.add('selected');
    selectedGender = selectedButton.textContent;
}

function populateDateDropdowns() {
    var currentYear = new Date().getFullYear();

    for (var i = 1; i <= 31; i++) {
        $('#dayOptions').append(`<li><a class="dropdown-item" href="#" onclick="selectDateOption('dayDisplay', ${i})">${i}</a></li>`);
    }

    months.forEach(function (month, index) {
        $('#monthOptions').append(`<li><a class="dropdown-item" href="#" onclick="selectDateOption('monthDisplay', '${month}')">${month}</a></li>`);
    });

    for (var i = currentYear; i >= 1900; i--) {
        $('#yearOptions').append(`<li><a class="dropdown-item" href="#" onclick="selectDateOption('yearDisplay', ${i})">${i}</a></li>`);
    }
}

function selectDateOption(elementId, value) {
    document.getElementById(elementId).textContent = value;
}

function showErrorMessage(fieldId, message) {
    console.log(`Showing error for ${fieldId}: ${message}`);
    let errorElement;
    
    if (fieldId === 'general') {
        errorElement = $('#errorMessage');
    } else {
        errorElement = $(`#${fieldId}`).siblings('.error-message');
    }
    
    if (errorElement.length) {
        errorElement.text(message).show();
    } else {
        console.error(`Error element not found for ${fieldId}`);
        $('#errorMessage').text(message).show();
    }
}

function clearErrorMessages() {
    console.log('Clearing all error messages');
    $('.error-message').text('').hide();
    $('#errorMessage').text('').hide();
}

function validateForm() {
    let isValid = true;
    clearErrorMessages();

    const name = $("#name").val().trim();
    const lastName = $("#lastName").val().trim();
    const email = $('#email').val().trim();
    const password = $('#Password').val();
    const confirmPassword = $('#ConfirmPassword').val();
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (name === "") {
        showErrorMessage('name', ERROR_MESSAGES.NAME_REQUIRED);
        isValid = false;
    }
    if (lastName === "") {
        showErrorMessage('lastName', ERROR_MESSAGES.LASTNAME_REQUIRED);
        isValid = false;
    }
    if (!email) {
        showErrorMessage('email', ERROR_MESSAGES.EMAIL_REQUIRED);
        isValid = false;
    } else if (!emailRegex.test(email)) {
        showErrorMessage('email', ERROR_MESSAGES.EMAIL_INVALID);
        isValid = false;
    }
    if (password === "") {
        showErrorMessage('Password', ERROR_MESSAGES.PASSWORD_REQUIRED);
        isValid = false;
    }
    if (password !== confirmPassword) {
        showErrorMessage('ConfirmPassword', ERROR_MESSAGES.PASSWORDS_MISMATCH);
        isValid = false;
    }

    return isValid;
}

function getBirthDate() {
    const day = $("#dayDisplay").text() !== "Día" ? $("#dayDisplay").text() : null;
    const month = $("#monthDisplay").text() !== "Mes" ? $("#monthDisplay").text() : null;
    const year = $("#yearDisplay").text() !== "Año" ? $("#yearDisplay").text() : null;

    if (day && month && year) {
        const monthNumber = months.indexOf(month) + 1;
        return `${year}-${monthNumber.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
    }

    return null;
}

function handleRegistrationError(xhr) {
    console.log('Handling registration error', xhr);
    if (xhr.responseJSON && xhr.responseJSON.errors) {
        const errors = xhr.responseJSON.errors;
        Object.keys(errors).forEach(key => {
            showErrorMessage(key, errors[key][0]);
        });
    } else if (xhr.responseJSON && xhr.responseJSON.errorCode) {
        const errorCode = xhr.responseJSON.errorCode;
        if (ERROR_MESSAGES[errorCode]) {
            showErrorMessage('general', ERROR_MESSAGES[errorCode]);
        } else {
            showErrorMessage('general', ERROR_MESSAGES.DEFAULT);
        }
    } else if (xhr.status === 409) {
        showErrorMessage('email', ERROR_MESSAGES.EMAIL_ALREADY_REGISTERED);
    } else {
        showErrorMessage('general', ERROR_MESSAGES.REGISTRATION_FAILED);
    }
}

function registerUser(userData) {
    $.ajax({
        url: "/api/User/register",
        type: "POST",
        contentType: "application/json",
        data: JSON.stringify(userData),
        success: (response) => {
            console.log('Registration successful', response);
            $("#successMessage").text("Registro realizado con éxito!").show();
            clearErrorMessages();
        },
        error: function (xhr, status, error) {
            console.log('Registration failed', xhr, status, error);
            handleRegistrationError(xhr);
        }
    });
}

$(document).ready(function () {
    populateDateDropdowns();

    $(".RegisterButton").on("click", function () {
        console.log('Register button clicked');
        if (!validateForm()) {
            console.log('Form validation failed');
            return;
        }

        const userData = {
            name: $("#name").val().trim(),
            lastName: $("#lastName").val().trim(),
            email: $('#email').val().trim(),
            password: $('#Password').val()
        };

        const birthDate = getBirthDate();
        if (birthDate) {
            userData.birthDate = birthDate;
        }

        if (selectedGender) {
            userData.gender = selectedGender;
        }

        registerUser(userData);
    });

    $('#registrationForm input').on('input', function() {
        console.log('Input changed, clearing errors');
        clearErrorMessages();
    });
});