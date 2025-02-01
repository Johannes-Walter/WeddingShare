const preventDefaults = event => {
    event.preventDefault();
    event.stopPropagation();
};

function displayLoader(message) {
    $('body').loading({
        theme: 'dark',
        message,
        stoppable: false,
        start: true
    });
}

function hideLoader() {
    $('body').loading('stop');
}

function uuidv4() {
    return "10000000-1000-4000-8000-100000000000".replace(/[018]/g, c =>
        (+c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> +c / 4).toString(16)
    );
}

function setCookie(cname, cvalue, hours) {
    const d = new Date();
    d.setTime(d.getTime() + (hours * 60 * 60 * 1000));
    document.cookie = `${cname}=${cvalue};expires=${d.toUTCString()};path=/`;
}

function getCookie(cname) {
    let ca = document.cookie.split(';');
    let name = `${cname}=`;

    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];

        while (c.charAt(0) == ' ') {
            c = c.substring(1);
        }

        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }

    return "";
}

function displayMessage(title, message, errors) {
    hideLoader();

    $('#alert-message-modal .modal-title').text(title);
    $('#alert-message-modal .modal-message').text(message);

    $('#alert-message-modal .modal-error').hide();
    if (errors && errors.length > 0) {
        var errorMessage = `<b>${localization.translate('Errors')}:</b>`;
        errorMessage += `<ul>`;
        errors.forEach((error) => {
            errorMessage += `<li>${error}</li>`;
        });
        errorMessage += `</ul>`;
        $('#alert-message-modal .modal-error').html(errorMessage);
        $('#alert-message-modal .modal-error').show();
    } else {
        $('#alert-message-modal .modal-error').text('');
    }

    $('#alert-message-modal').modal('show');
}

function displayIdentityCheck(required) {
    let buttons = [{
        Text: localization.translate('Identity_Check_Tell_Us'),
        Class: 'btn-success',
        Callback: function () {
            let name = $('#popup-modal-field-identity-name').val().trim();
            if (name !== undefined && name.length > 0) {
                const regex = /^[a-zA-Z-\s\-\']+$/;
                if (regex.test(name)) {
                    $.ajax({
                        url: '/Home/SetIdentity',
                        method: 'POST',
                        data: { name }
                    })
                        .done(data => {
                            $('#frmFileUpload').attr('data-identity-required', 'false');
                            window.location.reload();
                        })
                        .fail((xhr, error) => {
                            displayMessage(localization.translate('Identity_Check'), localization.translate('Identity_Check_Set_Failed'), [error]);
                        });
                } else {
                    displayMessage(localization.translate('Identity_Check_Invalid_Name'), localization.translate('Identity_Check_Invalid_Name_Msg'));
                }
            } else {
                displayMessage(localization.translate('Identity_Check_Invalid_Name'), localization.translate('Identity_Check_Invalid_Name_Msg'));
            }
        }
    }];

    if (!required) {
        buttons.push({
            Text: localization.translate('Identity_Check_Stay_Anonymous'),
            Callback: function () {
                $.ajax({
                    url: '/Home/SetIdentity',
                    method: 'POST',
                    data: { name: 'Anonymous' }
                })
                    .done(data => {
                        window.location.reload();
                    })
                    .fail((xhr, error) => {
                        displayMessage(localization.translate('Identity_Check'), localization.translate('Identity_Check_Set_Failed'), [error]);
                    });
            }
        });
    }

    displayPopup({
        Title: localization.translate('Identity_Check'),
        Fields: [{
            Id: 'identity-name',
            Name: localization.translate('Identity_Check_Name'),
            Value: '',
            Hint: localization.translate('Identity_Check_Hint'),
            Placeholder: localization.translate('Identity_Check_Placeholder')
        }],
        Buttons: buttons
    });
}

(function () {
    document.addEventListener('DOMContentLoaded', function () {
        $(document).off('click', '.change-theme').on('click', '.change-theme', function (e) {
            var currentTheme = getCookie('Theme');
            if (currentTheme === 'dark') {
                setCookie('Theme', 'default', 24);
            } else {
                setCookie('Theme', 'dark', 24);
            }

            window.location.reload();
        });

        $(document).off('click', '.change-identity').on('click', '.change-identity', function (e) {
            preventDefaults(e);
            displayPopup({
                Title: localization.translate('Identity_Check_Change_Identity'),
                Fields: [{
                    Id: 'identity-name',
                    Name: localization.translate('Identity_Check_Name'),
                    Value: $(this).data('identity'),
                    Hint: localization.translate('Identity_Check_Hint'),
                    Placeholder: localization.translate('Identity_Check_Placeholder')
                }],
                Buttons: [{
                    Text: localization.translate('Identity_Check_Change'),
                    Class: 'btn-success',
                    Callback: function () {
                        let name = $('#popup-modal-field-identity-name').val().trim();
                        if (name !== undefined && name.length > 0) {
                            const regex = /^[a-zA-Z-\s\-\']+$/;
                            if (regex.test(name)) {
                                $.ajax({
                                    url: '/Home/SetIdentity',
                                    method: 'POST',
                                    data: { name }
                                })
                                    .done(data => {
                                        window.location.reload();
                                    })
                                    .fail((xhr, error) => {
                                        displayMessage(localization.translate('Identity_Check_Change'), localization.translate('Identity_Check_Set_Failed'), [error]);
                                    });
                            } else {
                                displayMessage(localization.translate('Identity_Check_Invalid_Name'), localization.translate('Identity_Check_Invalid_Name_Msg'));
                            }
                        } else {
                            displayMessage(localization.translate('Identity_Check_Invalid_Name'), localization.translate('Identity_Check_Invalid_Name_Msg'));
                        }
                    }
                }, {
                    Text: localization.translate('Cancel')
                }]
            });
        });

        $(document).off('click', '.btn-reload').on('click', '.btn-reload', function () {
            window.location.reload();
        });

        $(document).off('click', '#btnGenerateGalleryName').on('click', '#btnGenerateGalleryName', function (e) {
            preventDefaults(e);
            $('input#gallery-id').val(uuidv4());
        });

        $(document).off('submit', '#frmSelectGallery').on('submit', '#frmSelectGallery', function (e) {
            preventDefaults(e);

            var galleryId = $('input#gallery-id').val();
            var secretKey = $('input#gallery-key').val();

            const regex = /^[a-zA-Z0-9\-\s-_~]+$/;
            if (galleryId && galleryId.length > 0 && regex.test(galleryId)) {
                $.ajax({
                    type: "POST",
                    url: '/Gallery/Login',
                    data: { id: galleryId, key: secretKey },
                    success: function (data) {
                        if (data.success && data.redirectUrl) {
                            window.location = data.redirectUrl;
                        } else {
                            displayMessage(localization.translate('Gallery'), localization.translate('Gallery_Invalid_Secret_Key'));
                        }
                    }
                });
            } else {
                displayMessage(localization.translate('Gallery'), localization.translate('Gallery_Invalid_Name'));
            }
        });

        $(document).off('submit', '#frmAdminLogin').on('submit', '#frmAdminLogin', function (e) {
            preventDefaults(e);

            var token = $('#frmAdminLogin input[name=\'__RequestVerificationToken\']').val();

            var username = $('#frmAdminLogin input.input-username').val();
            if (username === undefined || username.length === 0) {
                displayMessage(localization.translate('Login'), localization.translate('Login_Invalid_Username'));
                return;
            }

            var password = $('#frmAdminLogin input.input-password').val();
            if (password === undefined || password.length === 0) {
                displayMessage(localization.translate('Login'), localization.translate('Login_Invalid_Password'));
                return;
            }

            displayLoader(localization.translate('Loading'));

            $.ajax({
                url: '/Admin/Login',
                method: 'POST',
                data: { __RequestVerificationToken: token, Username: username, Password: password }
            })
                .done(data => {
                    hideLoader();

                    if (data.success === true) {
                        if (data.mfa === true) {
                            displayPopup({
                                Title: localization.translate('2FA'),
                                Fields: [{
                                    Id: '2fa-code',
                                    Name: localization.translate('Code'),
                                    Value: '',
                                    Hint: localization.translate('2FA_Code_Hint')
                                }],
                                Buttons: [{
                                    Text: localization.translate('Validate'),
                                    Class: 'btn-success',
                                    Callback: function () {
                                        let code = $('#popup-modal-field-2fa-code').val();

                                        $.ajax({
                                            type: "POST",
                                            url: '/Admin/ValidateMultifactorAuth',
                                            data: { __RequestVerificationToken: token, Username: username, Password: password, Code: code },
                                            success: function (data) {
                                                if (data.success === true) {
                                                    window.location = `/Admin`;
                                                } else if (data.message) {
                                                    displayMessage(localization.translate('Login'), localization.translate('Login_Failed'), [data.message]);
                                                } else {
                                                    displayMessage(localization.translate('Login'), localization.translate('Login_Failed'));
                                                }
                                            }
                                        });
                                    }
                                }, {
                                    Text: localization.translate('Close')
                                }]
                            });
                        } else {
                            window.location = `/Admin`;
                        }
                    } else if (data.message) {
                        displayMessage(localization.translate('Login'), localization.translate('Login_Failed'), [data.message]);
                    } else {
                        displayMessage(localization.translate('Login'), localization.translate('Login_Invalid_Details'));
                    }
                })
                .fail((xhr, error) => {
                    hideLoader();
                    displayMessage(localization.translate('Login'), localization.translate('Login_Failed'), [error]);
                });
        });

        $(document).off('click', 'button.btnDismissPopup').on('click', 'button.btnDismissPopup', function (e) {
            preventDefaults(e);
            hidePopup($(this).closest('.modal').attr('id'));
        });

    });
})();