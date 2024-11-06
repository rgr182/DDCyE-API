// Constants
const MONTHS = [
    "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
    "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
];

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

// State management
let selectedGender = null;

// Date handling functions
function populateDateDropdowns() {
    const currentYear = new Date().getFullYear();

    // Populate days
    for (let i = 1; i <= 31; i++) {
        $('#options-day').append(
            `<li><button class="dropdown-item" type="button" data-value="${i}">${i}</button></li>`
        );
    }

    // Populate months
    MONTHS.forEach((month, index) => {
        $('#options-month').append(
            `<li><button class="dropdown-item" type="button" data-value="${index + 1}">${month}</button></li>`
        );
    });

    // Populate years
    for (let i = currentYear; i >= 1900; i--) {
        $('#options-year').append(
            `<li><button class="dropdown-item" type="button" data-value="${i}">${i}</button></li>`
        );
    }
}

// Event Handlers
function handleDateSelection() {
    $('.dropdown-item').on('click', function(e) {
        e.preventDefault();
        const value = $(this).data('value');
        const text = $(this).text();
        const dropdownId = $(this).closest('.dropdown').find('span:first').attr('id');
        $(`#${dropdownId}`).text(text);
        $(`#${dropdownId}`).data('value', value);
    });
}

function handleGenderSelection() {
    $('.gender').on('click', function(e) {
        e.preventDefault();
        $('.gender').removeClass('selected');
        $(this).addClass('selected');
        selectedGender = $(this).data('gender');
    });
}

// Form validation
function validateForm() {
    clearMessages();
    const validationRules = [
        {
            condition: !$('#input-name').val().trim(),
            message: ERROR_MESSAGES.NAME_REQUIRED,
            field: 'input-name'
        },
        {
            condition: !$('#input-lastname').val().trim(),
            message: ERROR_MESSAGES.LASTNAME_REQUIRED,
            field: 'input-lastname'
        },
        {
            condition: !$('#input-email').val().trim(),
            message: ERROR_MESSAGES.EMAIL_REQUIRED,
            field: 'input-email'
        },
        {
            condition: !$('#input-password').val(),
            message: ERROR_MESSAGES.PASSWORD_REQUIRED,
            field: 'input-password'
        },
        {
            condition: $('#input-password').val() !== $('#input-confirm-password').val(),
            message: ERROR_MESSAGES.PASSWORDS_MISMATCH,
            field: 'input-confirm-password'
        }
    ];

    const errors = validationRules.filter(rule => rule.condition);
    if (errors.length > 0) {
        errors.forEach(error => showErrorMessage(error.field, error.message));
        return false;
    }

    return true;
}

// Message handling
function showErrorMessage(fieldId, message) {
    const errorElement = $(`#${fieldId}`).siblings('.error-message');
    if (errorElement.length) {
        errorElement.text(message).show();
    } else {
        $('#message-error').text(message).show();
    }
}

function showSuccessMessage(message) {
    $('#message-success').text(message).show();
}

function clearMessages() {
    $('.error-message').text('').hide();
    $('#message-error, #message-success').text('').hide();
}

// Data collection
function getBirthDate() {
    const day = $('#display-day').data('value');
    const month = $('#display-month').data('value');
    const year = $('#display-year').data('value');

    if (day && month && year) {
        return `${year}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
    }

    return null;
}

function collectFormData() {
    return {
        name: $('#input-name').val().trim(),
        lastName: $('#input-lastname').val().trim(),
        email: $('#input-email').val().trim(),
        password: $('#input-password').val(),
        birthDate: getBirthDate(),
        gender: selectedGender
    };
}

// API interaction
function registerUser(userData) {
    $.ajax({
        url: '/api/User/register',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(userData),
        success: (response) => {
            showSuccessMessage('Registro realizado con éxito!');
            if (response.redirectUrl) {
                window.location.href = response.redirectUrl;
            }
        },
        error: (xhr) => handleRegistrationError(xhr)
    });
}

function handleRegistrationError(xhr) {
    if (xhr.responseJSON?.errors) {
        Object.entries(xhr.responseJSON.errors).forEach(([key, [message]]) => {
            showErrorMessage(key, message);
        });
    } else if (xhr.responseJSON?.errorCode && ERROR_MESSAGES[xhr.responseJSON.errorCode]) {
        showErrorMessage('general', ERROR_MESSAGES[xhr.responseJSON.errorCode]);
    } else if (xhr.status === 409) {
        showErrorMessage('input-email', ERROR_MESSAGES.EMAIL_ALREADY_REGISTERED);
    } else {
        showErrorMessage('general', ERROR_MESSAGES.REGISTRATION_FAILED);
    }
}

// Initialization
$(document).ready(function() {
    populateDateDropdowns();
    handleDateSelection();
    handleGenderSelection();

    // Form submission
    $('#registration-form').on('submit', function(e) {
        e.preventDefault();
        if (validateForm()) {
            registerUser(collectFormData());
        }
    });

    // Back button
    $('.back-button').on('click', () => {
        window.location.href = BACK_BUTTON_URL;
    });

    // Clear messages on input
    $('input').on('input', clearMessages);
});