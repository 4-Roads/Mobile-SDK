(function($){

	var api = {
		register: function(options) {
			$.telligent.evolution.mobile.scrollable({
				region: 'content',
				load: function(pageIndex, success, error) {
					if ($(options.wrapper + ' div.data:last').data('hasmore')) {
						$.telligent.evolution.get({
							url: options.moreUrl,
							data: {
								w_pageIndex: pageIndex,
								w_contentId: options.contentId,
								w_contentTypeId: options.contentTypeId,
								w_typeId: options.typeId
							},
							success: function(r) {
								success(r);
							},
							error: function() {
								error();
							}
						});
					} else {
						success();
					}
				},
				complete: function(content) {
					content = $(content).css({ opacity: 0 });
					$(options.wrapper).find('div.post-list').append(content);
					content.evolutionTransform({ opacity: 1 }, { duration: 400 });
				}
			});
		}
	}

	$.telligent = $.telligent || {};
	$.telligent.evolution = $.telligent.evolution || {};
	$.telligent.evolution.widgets = $.telligent.evolution.widgets || {};
	$.telligent.evolution.widgets.likerList = api;

})(jQuery);