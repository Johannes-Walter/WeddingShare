(function () {
    document.addEventListener('DOMContentLoaded', function () {

        const triggerSelector = event => {
            const identityReqiured = $('#frmFileUpload').attr('data-identity-required') == 'true';
            if (identityReqiured) {
                displayIdentityCheck(true);
                return;
            }

            const zone = event.target.closest('.upload_drop') || false;
            const input = zone.querySelector('input[type="file"]') || false;
            input.click();
        }

        const highlight = event =>
            event.target.classList.add('highlight');

        const unhighlight = event =>
            event.target.classList.remove('highlight');

        const getInputAndGalleryRefs = element => {
            const zone = element.closest('.upload_drop') || false;
            const gallery = zone.querySelector('.upload_gallery') || false;
            const input = zone.querySelector('input[type="file"]') || false;
            return { input: input, gallery: gallery };
        }

        const handleDrop = event => {
            const dataRefs = getInputAndGalleryRefs(event.target);
            dataRefs.files = event.dataTransfer.files;
            handleFiles(dataRefs);
        }

        const eventHandlers = zone => {

            const dataRefs = getInputAndGalleryRefs(zone);

            if (!dataRefs.input) return;

            // Prevent default drag behaviors
            ;['dragenter', 'dragover', 'dragleave', 'drop'].forEach(event => {
                zone.addEventListener(event, preventDefaults, false);
                document.body.addEventListener(event, preventDefaults, false);
            });

            // Open file browser on drop area click
            ;['click', 'touch'].forEach(event => {
                zone.addEventListener(event, triggerSelector, false);
            });
            // Highlighting drop area when item is dragged over it
            ;['dragenter', 'dragover'].forEach(event => {
                zone.addEventListener(event, highlight, false);
            });
            ;['dragleave', 'drop'].forEach(event => {
                zone.addEventListener(event, unhighlight, false);
            });

            // Handle dropped files
            zone.addEventListener('drop', handleDrop, false);

            // Handle browse selected files
            dataRefs.input.addEventListener('change', event => {
                dataRefs.files = event.target.files;
                handleFiles(dataRefs);
            }, false);

        }

        // Initialise ALL dropzones
        const dropZones = document.querySelectorAll('.upload_drop');
        for (const zone of dropZones) {
            eventHandlers(zone);
        }

        // No 'image/gif' or PDF or webp allowed here, but it's up to your use case.
        // Double checks the input "accept" attribute
        const isImageFile = file => file.type.toLowerCase().startsWith('image/');
        const isVideoFile = file => file.type.toLowerCase().startsWith('video/');

        // Based on: https://flaviocopes.com/how-to-upload-files-fetch/
        const imageUpload = async dataRefs => {
            const identityReqiured = $('#frmFileUpload').attr('data-identity-required') == 'true';
            if (identityReqiured) {
                displayIdentityCheck(true);
                return;
            }

            // Multiple source routes, so double check validity
            if (!dataRefs.files || !dataRefs.input) {
                displayMessage(localization.translate('Upload'), localization.translate('Upload_No_Files_Detected'));
                return;
            }

            const token = $('#frmFileUpload input[name=\'__RequestVerificationToken\']').val();
            
            const galleryId = dataRefs.input.getAttribute('data-post-gallery-id');
            if (!galleryId) {
                displayMessage(localization.translate('Upload'), localization.translate('Upload_Invalid_Gallery_Detected'));
                return;
            }

            const url = dataRefs.input.getAttribute('data-post-url');
            if (!url) {
                displayMessage(localization.translate('Upload'), localization.translate('Upload_Invalid_Upload_Url'));
                return;
            }

            const secretKey = dataRefs.input.getAttribute('data-post-key');

            let uploadedCount = 0;
            let requiresReview = true;
            let errors = [];

            for (var i = 0; i < dataRefs.files.length; i++) {
                const formData = new FormData();
                formData.append('__RequestVerificationToken', token);
                formData.append('Id', galleryId);
                formData.append('SecretKey', secretKey);
                formData.append(dataRefs.files[i].name, dataRefs.files[i]);

                displayLoader(localization.translate('Upload_Progress', { index: i + 1, count: dataRefs.files.length }));

                let response = await postData({ url, formData });
                if (response !== undefined && response.success === true) {
                    uploadedCount++;
                    requiresReview = response.requiresReview;
                } else if (response.errors !== undefined && response.errors.length > 0) {
                    errors.push(response.errors);
                }
            }

            hideLoader();

            if (requiresReview) {
                displayMessage(localization.translate('Upload'), localization.translate('Upload_Success_Pending_Review', { count: uploadedCount }), errors);

                const formData = new FormData();
                formData.append('Id', galleryId);
                formData.append('SecretKey', secretKey);
                formData.append('Count', uploadedCount);

                postData({ url: '/Gallery/UploadCompleted', formData });
            } else {
                displayMessage(localization.translate('Upload'), localization.translate('Upload_Success', { count: uploadedCount }), errors);
            }
        }

        const postData = async request => {
            return fetch(request.url, {
                method: 'POST',
                body: request.formData
            })
                .then(response => response.json())
                .then(data => {
                    return data;
                });
        }

        // Handle both selected and dropped files
        const handleFiles = async dataRefs => {

            let files = [...dataRefs.files];

            // Remove unaccepted file types
            files = files.filter(item => {
                var isAllowed = isImageFile(item) || isVideoFile(item);
                if (!isAllowed) {
                    console.log(`File type '${item.type}' is not allowed. Filename: '${item.name}'`);
                }

                return isAllowed ? item : null;
            });

            if (!files.length) return;
            dataRefs.files = files;

            await imageUpload(dataRefs);
        }

        $(document).off('click', 'button.btnSaveQRCode').on('click', 'button.btnSaveQRCode', function (e) {
            preventDefaults(e);
            
            if ($(this).attr('disabled') == 'disabled') {
                return;
            }

            let galleryName = $(this).data('gallery-name');

            let link = document.createElement('a');
            link.download = `${galleryName}-qrcode.png`;
            link.href = $('#qrcode-download canvas')[0].toDataURL('image/png', 1.0).replace('image/png', 'image/octet-stream');
            link.click();
        });

        $(document).off('click', 'button.btnDownloadGallery').on('click', 'button.btnDownloadGallery', function (e) {
            preventDefaults(e);

            if ($(this).attr('disabled') == 'disabled') {
                return;
            }

            displayLoader(localization.translate('Loading'));

            let id = $(this).data('gallery-id');

            $.ajax({
                url: '/Gallery/DownloadGallery',
                method: 'POST',
                data: { Id: id }
            })
                .done(data => {
                    hideLoader();

                    if (data.success === true && data.filename) {
                        window.location.href = data.filename;
                    } else if (data.message) {
                        displayMessage(localization.translate('Download'), localization.translate('Download_Failed'), [data.message]);
                    } else {
                        displayMessage(localization.translate('Download'), localization.translate('Download_Failed'));
                    }
                })
                .fail((xhr, error) => {
                    hideLoader();
                    displayMessage(localization.translate('Download'), localization.translate('Download_Failed'), [error]);
                });
        });
        
        $(document).off('click', 'button.btnDeletePhoto').on('click', 'button.btnDeletePhoto', function (e) {
            preventDefaults(e);

            if ($(this).attr('disabled') == 'disabled') {
                return;
            }

            var id = $(this).data('photo-id');
            var name = $(this).data('photo-name');

            displayPopup({
                Title: localization.translate('Delete_Item'),
                Message: localization.translate('Delete_Item_Message', { name }),
                Fields: [{
                    Id: 'photo-id',
                    Value: id,
                    Type: 'hidden'
                }],
                Buttons: [{
                    Text: localization.translate('Delete'),
                    Class: 'btn-danger',
                    Callback: function () {
                        displayLoader(localization.translate('Loading'));

                        let id = $('#popup-modal-field-photo-id').val();
                        if (id == undefined || id.length == 0) {
                            displayMessage(localization.translate('Delete_Item'), localization.translate('Delete_Item_Id_Missing'));
                            return;
                        }

                        $.ajax({
                            url: '/Admin/DeletePhoto',
                            method: 'DELETE',
                            data: { id }
                        })
                            .done(data => {
                                if (data.success === true) {
                                    $(`tr[data-gallery-id=${id}]`).remove();
                                    displayMessage(localization.translate('Delete_Item'), localization.translate('Delete_Item_Success'));
                                } else if (data.message) {
                                    displayMessage(localization.translate('Delete_Item'), localization.translate('Delete_Item_Failed'), [data.message]);
                                } else {
                                    displayMessage(localization.translate('Delete_Item'), localization.translate('Delete_Item_Failed'));
                                }
                            })
                            .fail((xhr, error) => {
                                displayMessage(localization.translate('Delete_Item'), localization.translate('Delete_Item_Failed'), [error]);
                            });
                    }
                }, {
                    Text: localization.translate('Close')
                }]
            });
        });

    });
})();